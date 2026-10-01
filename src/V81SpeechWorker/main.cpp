#include <windows.h>
#include <io.h>

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <exception>
#include <limits>
#include <memory>
#include <new>
#include <string>
#include <vector>

#include "third_party/sherpa-onnx/c-api.h"

namespace {

// Must match SpeechWire and SpeechAudioBuffer in the plugin. Windows x64 uses
// little-endian IEEE float32 samples; audio is never written to disk.
constexpr std::uint32_t kMagic = 0x3154534c;
constexpr std::uint32_t kReady = 1;
constexpr std::uint32_t kRecognize = 2;
constexpr std::uint32_t kResult = 3;
constexpr std::uint32_t kError = 4;
constexpr std::int32_t kSampleRate = 48000;
constexpr std::int32_t kCapacity = kSampleRate * 30;
constexpr std::size_t kMaximumTextBytes = 32768;
static_assert(sizeof(float) == 4 && std::numeric_limits<float>::is_iec559);

// Fixed codes only: exception messages must never expose audio or transcripts.
struct WorkerError final : std::exception {
    explicit WorkerError(const char* value) noexcept : code(value) {}
    const char* what() const noexcept override { return code; }
    const char* code;
};

bool valid_handle(HANDLE value) noexcept {
    return value != nullptr && value != INVALID_HANDLE_VALUE;
}

class BinaryPipe final {
public:
    BinaryPipe() = default;
    ~BinaryPipe() { if (valid_handle(output_)) CloseHandle(output_); }
    BinaryPipe(const BinaryPipe&) = delete;
    BinaryPipe& operator=(const BinaryPipe&) = delete;

    void initialize() {
        const auto original_output = GetStdHandle(STD_OUTPUT_HANDLE);
        if (!valid_handle(original_output) || !DuplicateHandle(
                GetCurrentProcess(), original_output, GetCurrentProcess(),
                &output_, 0, FALSE, DUPLICATE_SAME_ACCESS)) {
            throw WorkerError("OutputUnavailable");
        }
        input_ = GetStdHandle(STD_INPUT_HANDLE);
        if (!valid_handle(input_)) throw WorkerError("InputUnavailable");

        // Keep an independent pipe handle for IPC, while routing both CRT and
        // Win32 stdout diagnostics from native libraries to stderr.
        const auto diagnostics = GetStdHandle(STD_ERROR_HANDLE);
        if (!valid_handle(diagnostics) || !SetStdHandle(STD_OUTPUT_HANDLE, diagnostics)
                || _dup2(_fileno(stderr), _fileno(stdout)) != 0) {
            throw WorkerError("DiagnosticsUnavailable");
        }
    }

    bool request(std::int64_t& session) {
        std::uint32_t magic = 0;
        if (!read_u32(magic, true)) return false;
        if (magic != kMagic) throw WorkerError("InvalidMagic");
        std::uint32_t kind = 0;
        read_u32(kind);
        if (kind != kRecognize) throw WorkerError("InvalidMessage");
        unsigned char bytes[8];
        read_exact(bytes, sizeof(bytes));
        std::uint64_t raw = 0;
        for (unsigned int i = 0; i < sizeof(bytes); ++i) {
            raw |= static_cast<std::uint64_t>(bytes[i]) << (i * 8);
        }
        std::memcpy(&session, &raw, sizeof(session));
        return true;
    }

    std::vector<float> samples() {
        std::uint32_t rate = 0;
        std::uint32_t count = 0;
        read_u32(rate);
        read_u32(count);
        if (rate != static_cast<std::uint32_t>(kSampleRate) || count == 0
                || count > static_cast<std::uint32_t>(kCapacity)) {
            throw WorkerError("InvalidAudioFormat");
        }
        std::vector<float> result(count);
        read_exact(result.data(), count * sizeof(float));
        for (const auto sample : result) {
            if (!std::isfinite(sample)) throw WorkerError("InvalidAudioSample");
        }
        return result;
    }

    void ready() { header(kReady, 0); }

    void result(std::int64_t session, const char* text) {
        const auto length = text_length(text);
        header(kResult, session);
        write_u32(static_cast<std::uint32_t>(length));
        write_exact(text, length);
    }

    void error(const char* code) noexcept {
        try {
            const auto length = text_length(code);
            header(kError, 0);
            write_u32(static_cast<std::uint32_t>(length));
            write_exact(code, length);
        } catch (...) { /* The parent may already have closed its pipe. */ }
    }

private:
    bool read_exact(void* destination, std::size_t length, bool allow_eof = false) {
        auto* bytes = static_cast<unsigned char*>(destination);
        std::size_t offset = 0;
        while (offset < length) {
            DWORD received = 0;
            const auto remaining = static_cast<DWORD>(length - offset);
            const auto success = ReadFile(input_, bytes + offset, remaining, &received, nullptr);
            if (!success) {
                const auto error = GetLastError();
                if (error != ERROR_BROKEN_PIPE && error != ERROR_HANDLE_EOF && error != ERROR_NO_DATA) {
                    throw WorkerError("InputFailure");
                }
                received = 0;
            }
            if (received == 0) {
                if (offset == 0 && allow_eof) return false;
                throw WorkerError("TruncatedRequest");
            }
            offset += received;
        }
        return true;
    }

    bool read_u32(std::uint32_t& value, bool allow_eof = false) {
        unsigned char bytes[4];
        if (!read_exact(bytes, sizeof(bytes), allow_eof)) return false;
        value = static_cast<std::uint32_t>(bytes[0])
                | (static_cast<std::uint32_t>(bytes[1]) << 8)
                | (static_cast<std::uint32_t>(bytes[2]) << 16)
                | (static_cast<std::uint32_t>(bytes[3]) << 24);
        return true;
    }

    void write_exact(const void* source, std::size_t length) {
        const auto* bytes = static_cast<const unsigned char*>(source);
        std::size_t offset = 0;
        while (offset < length) {
            DWORD written = 0;
            if (!WriteFile(output_, bytes + offset, static_cast<DWORD>(length - offset), &written, nullptr)
                    || written == 0) {
                throw WorkerError("OutputFailure");
            }
            offset += written;
        }
    }

    void write_u32(std::uint32_t value) {
        unsigned char bytes[4];
        for (unsigned int i = 0; i < sizeof(bytes); ++i) {
            bytes[i] = static_cast<unsigned char>(value >> (i * 8));
        }
        write_exact(bytes, sizeof(bytes));
    }

    void header(std::uint32_t kind, std::int64_t session) {
        write_u32(kMagic);
        write_u32(kind);
        std::uint64_t raw = 0;
        std::memcpy(&raw, &session, sizeof(raw));
        unsigned char bytes[8];
        for (unsigned int i = 0; i < sizeof(bytes); ++i) {
            bytes[i] = static_cast<unsigned char>(raw >> (i * 8));
        }
        write_exact(bytes, sizeof(bytes));
    }

    static std::size_t text_length(const char* text) {
        if (!text) throw WorkerError("InvalidResult");
        const auto length = strnlen_s(text, kMaximumTextBytes + 1);
        if (length > kMaximumTextBytes) throw WorkerError("ResultTooLong");
        if (length != 0 && MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
                text, static_cast<int>(length), nullptr, 0) == 0) {
            throw WorkerError("InvalidResultEncoding");
        }
        return length;
    }

    HANDLE input_ = nullptr; // Inherited handle, owned by the host process lifetime.
    HANDLE output_ = nullptr; // Duplicate retained while stdout is redirected.
};

std::wstring executable_directory() {
    std::vector<wchar_t> path(512);
    for (;;) {
        const auto length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0) throw WorkerError("ExecutablePathUnavailable");
        if (length < path.size()) {
            const std::wstring full_path(path.data(), length);
            const auto separator = full_path.find_last_of(L"\\/");
            if (separator == std::wstring::npos) throw WorkerError("ExecutablePathUnavailable");
            return full_path.substr(0, separator);
        }
        if (path.size() >= 32768) throw WorkerError("ExecutablePathTooLong");
        path.resize(path.size() * 2);
    }
}

bool regular_file(const wchar_t* path) noexcept {
    const auto attributes = GetFileAttributesW(path);
    return attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_DIRECTORY);
}

class Library final {
public:
    explicit Library(const std::wstring& directory) {
        const auto path = directory + L"\\sherpa-onnx-c-api.dll";
        // An absolute DLL path and restricted dependency search prevent unrelated
        // plugins or a developer's PATH from supplying another inference engine.
        module_ = LoadLibraryExW(path.c_str(), nullptr,
                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!module_) throw WorkerError("EngineLoadFailure");
    }
    ~Library() { FreeLibrary(module_); }
    Library(const Library&) = delete;
    Library& operator=(const Library&) = delete;

    template<class Function>
    Function symbol(const char* name) const {
        const auto address = GetProcAddress(module_, name);
        if (!address) throw WorkerError("EngineApiUnavailable");
        Function function;
        static_assert(sizeof(function) == sizeof(address));
        std::memcpy(&function, &address, sizeof(function));
        return function;
    }

private:
    HMODULE module_ = nullptr;
};

struct SherpaApi final {
    explicit SherpaApi(const std::wstring& directory) : library(directory) {
        const auto version = library.symbol<decltype(&SherpaOnnxGetVersionStr)>("SherpaOnnxGetVersionStr")();
        if (!version || std::strcmp(version, "1.13.8") != 0) throw WorkerError("EngineVersionMismatch");
        create_recognizer = library.symbol<decltype(create_recognizer)>("SherpaOnnxCreateOfflineRecognizer");
        destroy_recognizer = library.symbol<decltype(destroy_recognizer)>("SherpaOnnxDestroyOfflineRecognizer");
        create_stream = library.symbol<decltype(create_stream)>("SherpaOnnxCreateOfflineStream");
        destroy_stream = library.symbol<decltype(destroy_stream)>("SherpaOnnxDestroyOfflineStream");
        accept_waveform = library.symbol<decltype(accept_waveform)>("SherpaOnnxAcceptWaveformOffline");
        decode = library.symbol<decltype(decode)>("SherpaOnnxDecodeOfflineStream");
        get_result = library.symbol<decltype(get_result)>("SherpaOnnxGetOfflineStreamResult");
        destroy_result = library.symbol<decltype(destroy_result)>("SherpaOnnxDestroyOfflineRecognizerResult");
    }

    Library library;
    decltype(&SherpaOnnxCreateOfflineRecognizer) create_recognizer = nullptr;
    decltype(&SherpaOnnxDestroyOfflineRecognizer) destroy_recognizer = nullptr;
    decltype(&SherpaOnnxCreateOfflineStream) create_stream = nullptr;
    decltype(&SherpaOnnxDestroyOfflineStream) destroy_stream = nullptr;
    decltype(&SherpaOnnxAcceptWaveformOffline) accept_waveform = nullptr;
    decltype(&SherpaOnnxDecodeOfflineStream) decode = nullptr;
    decltype(&SherpaOnnxGetOfflineStreamResult) get_result = nullptr;
    decltype(&SherpaOnnxDestroyOfflineRecognizerResult) destroy_result = nullptr;
};

SherpaOnnxOfflineRecognizerConfig recognizer_config() {
    SherpaOnnxOfflineRecognizerConfig config{};
    // Match the v1.13.8 C# config constructor defaults used by the old worker.
    // sherpa-onnx resamples the 48 kHz capture to the model's 16 kHz features.
    config.feat_config.sample_rate = 16000;
    config.feat_config.feature_dim = 80;
    config.model_config.sense_voice.model = "models/model.int8.onnx";
    config.model_config.sense_voice.language = "auto";
    config.model_config.sense_voice.use_itn = 1;
    config.model_config.tokens = "models/tokens.txt";
    config.model_config.num_threads = 1;
    config.model_config.provider = "cpu";
    config.model_config.debug = 0;
    config.model_config.modeling_unit = "cjkchar";
    config.lm_config.scale = 0.5f;
    config.decoding_method = "greedy_search";
    config.max_active_paths = 4;
    config.hotwords_score = 1.5f;
    return config;
}

} // namespace

int main() {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    BinaryPipe pipe;
    try {
        pipe.initialize();
        if (!SetPriorityClass(GetCurrentProcess(), BELOW_NORMAL_PRIORITY_CLASS)) {
            throw WorkerError("PriorityUnavailable");
        }
        const auto directory = executable_directory();
        // Wide-character Windows calls preserve Chinese and space-containing
        // installation directories. The model API receives only relative ASCII.
        if (!SetCurrentDirectoryW(directory.c_str())) throw WorkerError("WorkerDirectoryUnavailable");
        if (!regular_file(L"models\\model.int8.onnx") || !regular_file(L"models\\tokens.txt")) {
            throw WorkerError("ModelUnavailable");
        }
        SherpaApi api(directory);
        const auto config = recognizer_config();
        const std::unique_ptr<const SherpaOnnxOfflineRecognizer, decltype(api.destroy_recognizer)> recognizer(
                api.create_recognizer(&config), api.destroy_recognizer);
        if (!recognizer) throw WorkerError("RecognizerUnavailable");
        pipe.ready();
        for (;;) {
            std::int64_t session = 0;
            if (!pipe.request(session)) return 0; // EOF between messages is an orderly shutdown.
            const auto samples = pipe.samples();
            const std::unique_ptr<const SherpaOnnxOfflineStream, decltype(api.destroy_stream)> stream(
                    api.create_stream(recognizer.get()), api.destroy_stream);
            if (!stream) throw WorkerError("StreamUnavailable");
            api.accept_waveform(stream.get(), kSampleRate, samples.data(), static_cast<std::int32_t>(samples.size()));
            api.decode(recognizer.get(), stream.get());
            const std::unique_ptr<const SherpaOnnxOfflineRecognizerResult, decltype(api.destroy_result)> result(
                    api.get_result(stream.get()), api.destroy_result);
            if (!result) throw WorkerError("ResultUnavailable");
            pipe.result(session, result->text);
        }
    } catch (const WorkerError& error) {
        pipe.error(error.code);
    } catch (const std::bad_alloc&) {
        pipe.error("AllocationFailure");
    } catch (...) {
        pipe.error("WorkerFailure");
    }
    return 1;
}

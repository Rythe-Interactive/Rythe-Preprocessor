#pragma once
#define SPDLOG_HEADER_ONLY
#include <sstream>
#include <thread>
#include <shared_mutex>
#include <chrono>
#include <string_view>

#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/pattern_formatter.h>

#include <cppast/diagnostic_logger.hpp>

namespace ast::log
{
    using logger_ptr = std::shared_ptr<spdlog::logger>;

    /** @brief selects the severity you want to filter for or print with */
    enum class severity
    {
        trace,   // lowest severity
        debug,
        info,
        warn,
        error,
        fatal // highest severity
    };
}

namespace fmt
{
    template <>
    struct formatter<::ast::log::severity>
    {

        constexpr const char* parse(format_parse_context& ctx)
        {
            auto it = ctx.begin(), end = ctx.end();

            if (!it)
                return nullptr;

            if (it != end && *it != '}')
                throw format_error("invalid format");
            return it++;
        }

        template <typename FormatContext>
        auto format(const ::ast::log::severity& severity, FormatContext& ctx)
        {
            switch (severity)
            {
            case ::ast::log::severity::trace:
                return format_to(ctx.out(), "trace");
            case ::ast::log::severity::debug:
                return format_to(ctx.out(), "debug");
            case ::ast::log::severity::info:
                return format_to(ctx.out(), "info");
            case ::ast::log::severity::warn:
                return format_to(ctx.out(), "warn");
            case ::ast::log::severity::error:
                return format_to(ctx.out(), "error");
            case ::ast::log::severity::fatal:
                return format_to(ctx.out(), "fatal");
            default:
                return format_to(ctx.out(), "UNKNOWN");
            }
        }
    };
}

namespace ast::log
{
    struct impl
    {
        static logger_ptr logger;
        static std::atomic<severity> currentSeverity;
        const static std::chrono::time_point<std::chrono::high_resolution_clock> genesis;

        static std::shared_mutex threadNamesLock;
        static std::unordered_map<std::thread::id, std::string> threadNames;
    };

    extern void init();

    constexpr severity severity_trace = severity::trace;
    constexpr severity severity_debug = severity::debug;
    constexpr severity severity_info = severity::info;
    constexpr severity severity_warn = severity::warn;
    constexpr severity severity_error = severity::error;
    constexpr severity severity_fatal = severity::fatal;

    constexpr spdlog::level::level_enum args2spdlog(severity s)
    {
        switch (s)
        {
        case severity_trace:return spdlog::level::trace;
        case severity_debug:return spdlog::level::debug;
        case severity_info: return spdlog::level::info;
        case severity_warn: return spdlog::level::warn;
        case severity_error:return spdlog::level::err;
        case severity_fatal:return spdlog::level::critical;
        }
        return spdlog::level::err;
    }

    /** @brief prints a log line, using the specified `severity`
     *  @param s The severity you wan't to report this log with
     *  @param format The format string you want to print
     *  @param a The arguments to the format string
     *  @note This uses fmt lib style syntax check
     *         https://fmt.dev/latest/syntax.html
     */
    template <class... Args, class FormatString>
    void println(severity s, const FormatString& format, Args&&... a)
    {
        impl::logger->log(args2spdlog(s), format, std::forward<Args>(a)...);
    }

    /** @brief prints a log line, using the specified `severity`
     *  @param level selects the severity level you are interested in
     */
    inline void filter(severity level) noexcept
    {
        impl::logger->set_level(args2spdlog(level));
        impl::currentSeverity.store(level, std::memory_order_seq_cst);
    }

    inline severity current_filter() noexcept
    {
        return impl::currentSeverity.load(std::memory_order_relaxed);
    }

    inline void set_thread_name(const std::string_view& name)
    {
        std::lock_guard<std::shared_mutex> wguard(impl::threadNamesLock);
        impl::threadNames[std::this_thread::get_id()] = std::string(name);
    }

    inline std::string get_thread_name()
    {
        std::lock_guard<std::shared_mutex> wguard(impl::threadNamesLock);
        return impl::threadNames[std::this_thread::get_id()];
    }

    /** @brief same as println but with severity = trace */
    template<class... Args, class FormatString>
    void trace(const FormatString& format, Args&&... a)
    {
        println(severity::trace, format, std::forward<Args>(a)...);
    }

    /** @brief same as println but with severity = debug */
    template<class... Args, class FormatString>
    void debug(const FormatString& format, Args&&...a)
    {
        println(severity::debug, format, std::forward<Args>(a)...);
    }

    /** @brief same as println but with severity = info */
    template<class... Args, class FormatString>
    void info(const FormatString& format, Args&&...a)
    {
        println(severity::info, format, std::forward<Args>(a)...);
    }

    /** @brief same as println but with severity = warn */
    template<class... Args, class FormatString>
    void warn(const FormatString& format, Args&&...a)
    {
        println(severity::warn, format, std::forward<Args>(a)...);
    }

    /** @brief same as println but with severity = error */
    template<class... Args, class FormatString>
    void error(const FormatString& format, Args&&...a)
    {
        println(severity::error, format, std::forward<Args>(a)...);
    }

    /** @brief same as println but with severity = fatal */
    template<class... Args, class FormatString>
    void fatal(const FormatString& format, Args&&...a)
    {
        println(severity::fatal, format, std::forward<Args>(a)...);
    }

    class cppast_diagnostic_logger final : public cppast::diagnostic_logger
    {
    public:
        using cppast::diagnostic_logger::diagnostic_logger;

    private:
        bool do_log(const char* source, const cppast::diagnostic& d) const override;
    };
}

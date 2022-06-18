#include "logger.hpp"
#include "utils.hpp"

namespace ast::log
{
	logger_ptr impl::logger = spdlog::stdout_color_mt("console-logger");
	const std::chrono::time_point<std::chrono::high_resolution_clock> impl::genesis = std::chrono::high_resolution_clock::now();

	std::atomic<severity> impl::currentSeverity;

	std::shared_mutex impl::threadNamesLock;
	std::unordered_map<std::thread::id, std::string> impl::threadNames;

	/** @class genesis_formatter_flag
	 *  @brief Custom formatter flag that prints the time since the engine started in seconds.milliseconds
	 */
	class genesis_formatter_flag : public spdlog::custom_flag_formatter
	{
	public:
		void format(const spdlog::details::log_msg& msg, const std::tm& tm_time, spdlog::memory_buf_t& dest) override
		{
			//get seconds since engine start
			const auto now = std::chrono::high_resolution_clock::now();
			const auto time_since_genesis = now - impl::genesis;
			const auto seconds = std::chrono::duration_cast<std::chrono::duration<float, std::ratio<1, 1>>>(time_since_genesis).count();

			//convert to "--s.ms---"
			const auto str = std::to_string(seconds);

			//append to data
			dest.append(str.data(), str.data() + str.size());

		}

		//generates a new formatter flag
		[[nodiscard]] std::unique_ptr<custom_flag_formatter> clone() const override
		{
			return spdlog::details::make_unique<genesis_formatter_flag>();
		}
	};

	/** @class thread_name_formatter_flag
	 *  @brief Prints the name of the thread (if available) and otherwise the the TID.
	 */
	class thread_name_formatter_flag : public spdlog::custom_flag_formatter
	{
		void format(const spdlog::details::log_msg& msg, const std::tm& tm_time, spdlog::memory_buf_t& dest) override
		{
			//std::string thread_ident;
			thread_local static std::string* thread_ident;

			if (!thread_ident)
			{
				bool found;

				{
					std::shared_lock<std::shared_mutex> guard(impl::threadNamesLock);

					if ((found = impl::threadNames.count(std::this_thread::get_id())))
					{
						thread_ident = &impl::threadNames.at(std::this_thread::get_id());
					}
				}

				if (!found)
				{
					std::ostringstream oss;
					oss << std::this_thread::get_id();
					{
						std::lock_guard<std::shared_mutex> wguard(impl::threadNamesLock);
						thread_ident = &impl::threadNames[std::this_thread::get_id()];
					}
					*thread_ident = oss.str();
				}
			}

			dest.append(thread_ident->data(), thread_ident->data() + thread_ident->size());
		}

		[[nodiscard]] std::unique_ptr<custom_flag_formatter> clone() const override
		{
			return spdlog::details::make_unique<thread_name_formatter_flag>();

		}
	};

	void init()
	{
		auto f = std::make_unique<spdlog::pattern_formatter>();

		f->add_flag<thread_name_formatter_flag>('f');
		f->add_flag<genesis_formatter_flag>('*');
		f->set_pattern("T+ %* [%^%=7l%$] [%=13!f] : %v");

		impl::logger->set_formatter(std::move(f));

		filter(severity::trace);
	}

	bool cppast_diagnostic_logger::do_log(const char* source, const cppast::diagnostic& d) const
	{
		if (get_thread_name().empty())
			set_thread_name("cppast");

		auto loc = d.location.to_string();
		severity s;

		switch (d.severity)
		{
		case cppast::severity::debug:
			s = severity_trace;
			break;
		case cppast::severity::info:
			s = severity_info;
			break;
		case cppast::severity::warning:
			s = severity_warn;
			break;
		case cppast::severity::error:
			s = severity_error;
			break;
		case cppast::severity::critical:
			s = severity_fatal;
			break;
		default:
			s = severity_error;
			break;
		}

		if (loc.empty())
			println(s, "[{}] {}", source, d.message);
		else if (std::strcmp(source, "libclang") != 0)
			println(s, "[{}] {}{}", source, loc, d.message);

		return true;
	}
}

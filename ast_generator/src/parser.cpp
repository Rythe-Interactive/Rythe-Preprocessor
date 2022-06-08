#include "parser.hpp"
#include "utils.hpp"

#include <vector>
#include <filesystem>

namespace ast
{
	Parser::Parser(const parser_settings& settings) : m_settings(settings), m_index(), m_logger(settings.verbose), m_parser(type_safe::ref(m_index), type_safe::ref(m_logger)) {}

	class cppast_compile_config : public cppast::libclang_compile_config
	{
	public:
		using cppast::libclang_compile_config::libclang_compile_config;

		cppast_compile_config() : cppast::libclang_compile_config()
		{
			set_flags(cppast::cpp_standard::cpp_17);
		}

		void add_manual_flag(const std::string& flag)
		{
			add_flag(flag);
		}
	};

	bool Parser::parse() noexcept
	{
		try
		{
			std::string warnings[] = {
				"-Wall",
				"-Wmicrosoft",
				"-Wno-invalid-token-paste",
				"-Wno-unknown-pragmas",
				"-Wno-unused-value",
			};

			std::string features[] = {
				"ms-compatibility-version=19.10",
			};

			std::pair<std::string, std::string> defines[] = {
				{"UNICODE", ""},
				{"_UNICODE", ""},
				{"_MT", ""},
				{"_DLL", ""},
				{"_DEBUG", ""},
				{"_CONSOLE", ""},
				{"_DEBUG_FUNCTIONAL_MACHINERY", ""}
			};

			std::string systemIncludes[] = {
				"C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/MSVC/14.31.31103/include",
				"C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/MSVC/14.31.31103/atlmfc/include",
				"C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Auxiliary/VS/include",
				"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/ucrt",
				"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/um",
				"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/shared",
				"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/winrt",
				"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/cppwinrt",
				"./deps/include"
			};

			std::string includes[] = {
				"./src"
			};

			cppast_compile_config config;
			for (auto& warn : warnings)
				config.add_manual_flag(warn);

			for (auto& feature : features)
				config.enable_feature(feature);

			for (auto& [name, definition] : defines)
				config.define_macro(name, definition);

			for (auto& si : systemIncludes)
				config.add_manual_flag("-isystem\"" + sanitise_path(si) + '\"');

			for (auto& include : includes)
				config.add_include_dir('\"' + sanitise_path(include) + '\"');

			config.fast_preprocessing(true);
			config.remove_comments_in_macro(true);

			std::vector<std::string> files;

			auto searchDir = [&](const std::filesystem::path& dir, auto&& search) -> void
			{
				for (const auto& entry : std::filesystem::directory_iterator(dir)) {
					const auto filenameStr = entry.path().filename().string();
					if (entry.is_directory())
						search(entry.path(), search);
					else if (entry.is_regular_file())
					{
						auto extension = entry.path().extension();
						if (extension == ".hpp" || extension == ".h")
							files.push_back(sanitise_path(entry.path().string()));
					}
				}
			};

			searchDir(m_settings.buildDirectory, searchDir);

			std::vector<std::thread> threadPool;
			threadPool.reserve(files.size());

			std::atomic_bool executionSuccess = { true };

			std::atomic_int threads;
			for (auto& path : files)
				threadPool.emplace_back([&]()
					{
						log::set_thread_name("cppast" + std::to_string(threads.fetch_add(1, std::memory_order::memory_order_relaxed)));
						try
						{
							m_parser.parse(path, config);
						}
						catch (cppast::libclang_error& ex)
						{
							log::error("Fatal libclang error: {}", ex.what());
							executionSuccess.store(false, std::memory_order_relaxed);
						}
					});

			for (auto& thread : threadPool)
				thread.join();			

			if (!executionSuccess.load(std::memory_order_relaxed))
				return false;

			if (m_parser.error()) // error has been logged to stderr
				return false;

			return true;
		}
		catch (std::exception& ex)
		{
			log::error("Fatal error: {}", ex.what());
			return false;
		}
	}
}

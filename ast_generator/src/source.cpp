#include <argh.h>
#include <iostream>
#include "logger.hpp"
#include "parser.hpp"
#include "utils.hpp"

#include <cppast/visitor.hpp>

int main(int argc, char** argv)
{
	using namespace ast;
	argh::parser args(argc, argv);

	if (args[{"-h", "-help"}])
	{
		std::cout << "HELP\n";
		return 0;
	}

	log::init();
	log::set_thread_name("main");
	log::filter(log::severity_info);

	if (auto argStream = args({ "-v", "-l", "-log" }))
	{
		try
		{
			auto argVal = std::stoi(argStream.str());
			log::severity logSeverity = static_cast<log::severity>(argVal < 0 ? 0 : (argVal > 5 ? 5 : argVal));
			log::info("Set logging level to: {}", logSeverity);
			log::filter(logSeverity);
		}
		catch (...)
		{
			log::error("Failed to parse logging settings, reverting to safe mode default: {}", log::severity_trace);
			log::filter(log::severity_trace);
		}
	}

	if (static_cast<int>(log::current_filter()) <= static_cast<int>(log::severity_debug))
	{
		log::debug("Execution dir: {}", std::filesystem::current_path().string());

		{
			std::string flags;
			for (auto& item : args.flags())
			{
				flags += item + ", ";
			}

			log::debug("Flags: {}", flags.substr(0, flags.size() - 2));
		}

		{
			std::string params;
			for (auto& [item, value] : args.params())
			{
				params += item + "=" + value + ", ";
			}

			log::debug("Params: {}", params.substr(0, params.size() - 2));

		}
	}

	if (!args({ "-d", "-dir" }))
	{
		log::error("Missing build directory.");
		return -1;
	}

	parser_settings pSettings;

	pSettings.buildDirectory = sanitise_path(args({ "-d", "-dir" }).str());
	
	log::debug("Build dir: {}", pSettings.buildDirectory);

	Parser parser(pSettings);

	if (!parser.parse())
		return -1;

	for (auto& file : parser.files())
	{
		std::string prefix;
		// visit each entity in the file
		cppast::visit(file, [&](const cppast::cpp_entity& e, cppast::visitor_info info)
			{
				if (info.event == cppast::visitor_info::container_entity_exit) // exiting an old container
					prefix.pop_back();
				else if (info.event == cppast::visitor_info::container_entity_enter)
					// entering a new container
				{
					log::info("{}'{}' - {}", prefix, e.name(), cppast::to_string(e.kind()));
					prefix += "\t";
				}
				else // if (info.event == cppast::visitor_info::leaf_entity) // a non-container entity
					log::info("{}'{}' - {}", prefix, e.name(), cppast::to_string(e.kind()));
			});
	}

	return 0;
}

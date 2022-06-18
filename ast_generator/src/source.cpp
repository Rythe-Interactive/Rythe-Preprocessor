#include <argh.h>
#include <iostream>
#include "logger.hpp"
#include "parser.hpp"
#include "utils.hpp"

#include <cppast/visitor.hpp>
#include <cppast/cpp_template.hpp>

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
	pSettings.verbose = static_cast<int>(log::current_filter()) <= static_cast<int>(log::severity_trace);

	log::debug("Build dir: {}", pSettings.buildDirectory);

	Parser parser(pSettings);

	if (!parser.parse())
		return -1;

	auto getTypeName = [](const cppast::cpp_non_type_template_parameter* param)
	{
		if (!param)
			return std::string();

		auto& type = param->type();
		auto kind = type.kind();

		switch (kind)
		{
		case cppast::cpp_type_kind::builtin_t:
		{
			auto* ptr = dynamic_cast<const cppast::cpp_builtin_type*>(&type);
			if (!ptr)
				return std::string();

			return std::string(cppast::to_string(ptr->builtin_type_kind()));
		}
		case cppast::cpp_type_kind::user_defined_t:
		{
			auto* ptr = dynamic_cast<const cppast::cpp_user_defined_type*>(&type);
			if (!ptr)
				return std::string();

			return ptr->entity().name();
		}
		case cppast::cpp_type_kind::auto_t:
			return std::string("auto");
		case cppast::cpp_type_kind::decltype_t:
			return std::string("decltype");
		case cppast::cpp_type_kind::decltype_auto_t:
			return std::string("decltype_auto");
		case cppast::cpp_type_kind::cv_qualified_t:
			return std::string("cv_qualified");
		case cppast::cpp_type_kind::pointer_t:
			return std::string("pointer");
		case cppast::cpp_type_kind::reference_t:
			return std::string("reference");
		case cppast::cpp_type_kind::array_t:
			return std::string("array");
		case cppast::cpp_type_kind::function_t:
			return std::string("function");
		case cppast::cpp_type_kind::member_function_t:
			return std::string("member_function");
		case cppast::cpp_type_kind::member_object_t:
			return std::string("member_object");
		case cppast::cpp_type_kind::template_parameter_t:
			return std::string("template_parameter");
		case cppast::cpp_type_kind::template_instantiation_t:
			return std::string("template_instantiation");
		case cppast::cpp_type_kind::dependent_t:
			return std::string("dependent");
		case cppast::cpp_type_kind::unexposed_t:
			return std::string("unexposed");
		}
		return std::string("UNKNOWN");
	};

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
					auto kind = e.kind();

					switch (kind)
					{
					case cppast::cpp_entity_kind::alias_template_t:
					case cppast::cpp_entity_kind::variable_template_t:
					case cppast::cpp_entity_kind::function_template_t:
					case cppast::cpp_entity_kind::function_template_specialization_t:
					case cppast::cpp_entity_kind::class_template_t:
					case cppast::cpp_entity_kind::class_template_specialization_t:
					{
						auto* templateEnt = dynamic_cast<const cppast::cpp_template*>(&e);
						if (templateEnt)
						{
							std::string paramters;
							for (auto& param : templateEnt->parameters())
							{
								auto paramKind = param.kind();

								if (paramKind == cppast::cpp_entity_kind::template_type_parameter_t)
									paramters += "typename";
								else if (paramKind == cppast::cpp_entity_kind::template_template_parameter_t)
									paramters += "template";
								else if (paramKind == cppast::cpp_entity_kind::non_type_template_parameter_t)
									paramters += getTypeName(dynamic_cast<const cppast::cpp_non_type_template_parameter*>(&param));

								if (param.is_variadic())
									paramters += "...";

								paramters += " " + param.name() + ", ";
							}

							log::info("{}'{}' - {}: template<{}>", prefix, e.name(), cppast::to_string(kind), paramters.substr(0, paramters.size() - 2));
							break;
						}
					}
					default:
						log::info("{}'{}' - {}", prefix, e.name(), cppast::to_string(kind));
					}

					prefix += "\t";
				}
				else // if (info.event == cppast::visitor_info::leaf_entity) // a non-container entity
					log::info("{}'{}' - {}", prefix, e.name(), cppast::to_string(e.kind()));
			});
	}

	return 0;
}

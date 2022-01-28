#include <iostream>
#include <cppast/code_generator.hpp> 
#include <cppast/libclang_parser.hpp>
#include <cppast/visitor.hpp>


namespace parser
{
	using namespace cppast;
	typedef cpp_entity_index ent_index;
	typedef libclang_compile_config compile_config;

	std::unique_ptr<cpp_file> parse_file(const compile_config& config, const diagnostic_logger& logger, const std::string& fileName)
	{
		ent_index idx;
		libclang_parser parser(type_safe::ref(logger));
		auto file = parser.parse(idx, fileName, config);
		if (parser.error())
			return nullptr;
		return file;
	}

	void print_entity(const cpp_entity& e)
	{
		std::cout << !e.name().empty() ? e.name() : "<anonymous>";
	}
}

int main()
{
	cppast::libclang_compile_config config;
	cppast::stderr_diagnostic_logger logger;
	auto file = parser::parse_file(config, logger, "");
}

#include <iostream>
#include <cppast/cpp_member_variable.hpp>
#include <cppast/cpp_class.hpp>
#include <cppast/cpp_type.hpp>
#include <cppast/libclang_parser.hpp>
#include <cppast/visitor.hpp>


namespace parser
{
	using namespace cppast;
	typedef cpp_entity_index ent_index;
	typedef libclang_compile_config compile_config;

	bool is_c_string(const cpp_type& type)
	{
		if (type.kind() != cpp_type_kind::pointer_t)
			return false;

		// get the pointee
		auto& pointee = remove_cv(static_cast<const cpp_pointer_type&>(type).pointee());
		if (pointee.kind() != cpp_type_kind::builtin_t)
			return false;

		// check the builtin type kind
		auto builtin = static_cast<const cpp_builtin_type&>(pointee).builtin_type_kind();
		return builtin == cpp_char || builtin == cpp_char16 || builtin == cpp_char32 || builtin == cpp_wchar;
	}

	void generate_member_code(std::ostream& out, const cpp_member_variable& member)
	{
		auto& type = remove_cv(member.type());

		if (auto attr = has_attribute(member,"legion::reflectable"))
		{
			out << " " << attr.value().arguments().value().as_string() << ";\n";
		}
		else if (type.kind() == cpp_type_kind::builtin_t)
		{
			out << member.name() << "\n";
		}
		else if (type.kind() == cpp_type_kind::user_defined_t)
		{
			out << member.name() << "\n";
		}
		else if (is_c_string(type))
		{
			// generate another hypothetical member function call
			out << member.name() << "\n";
		}
		else
			throw std::invalid_argument("cannot serialize member " + member.name());
	}

	auto entity_filter = [](const cpp_entity& e) 
	{
		return (!is_templated(e) && e.kind() == cpp_entity_kind::class_t && cppast::is_definition(e) && has_attribute(e, "legion::reflectable")) || e.kind() == cpp_entity_kind::class_t;
	};
	auto func_generator = [](const cpp_entity& e, const visitor_info& info) 
	{
		if (e.kind() == cpp_entity_kind::class_t && !info.is_old_entity())
		{
			auto& class_ = static_cast<const cpp_class&>(e);

			std::cout << "void make_prototype(const " << class_.name() << "& obj) \n {";

			for (auto& member : class_)
			{
				if (member.kind() == cpp_entity_kind::member_variable_t)
					generate_member_code(std::cout,static_cast<const cpp_member_variable&>(member));
			}
			std::cout << "\n}\n";
		}
	};

	std::unique_ptr<cpp_file> parse_file(const compile_config& config, const diagnostic_logger& logger, const std::string& fileName)
	{
		ent_index idx;
		libclang_parser parser(type_safe::ref(logger));
		auto file = parser.parse(idx, fileName, config);
		if (parser.error())
			return nullptr;
		return file;
	}

	void print_file(const cpp_file& file)
	{
		visit(file, entity_filter, func_generator);
	}
}

int main()
{
	cppast::libclang_compile_config config;
	cppast::stderr_diagnostic_logger logger;
	auto file = parser::parse_file(config, logger, "data/Reflectable_test.cpp");
}

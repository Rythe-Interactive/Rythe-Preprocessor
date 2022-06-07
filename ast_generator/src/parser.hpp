#pragma once
#include <string>
#include <cppast/libclang_parser.hpp>

namespace ast
{
	struct parser_settings
	{
		std::string buildDirectory;
	};

	class Parser
	{
	public:
		Parser(const parser_settings& settings);

		bool parse() noexcept;

		[[nodiscard]] auto files() const noexcept { return m_parser.files(); }

	private:
		parser_settings m_settings;
		cppast::cpp_entity_index m_index;
		cppast::simple_file_parser<cppast::libclang_parser> m_parser;
	};
}
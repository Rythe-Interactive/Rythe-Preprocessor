#include "parser.hpp"
#include "logger.hpp"

namespace ast
{
	Parser::Parser(const parser_settings& settings) : m_settings(settings), m_index(), m_parser(type_safe::ref(m_index)) {}

	bool Parser::parse() noexcept
	{
		try
		{
			cppast::libclang_compilation_database database(m_settings.buildDirectory);

			try
			{
				cppast::parse_database(m_parser, database); // parse all files in the database
			}
			catch (cppast::libclang_error& ex)
			{
				log::error("Fatal libclang error: {}", ex.what());
				return false;
			}

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

#pragma once
#include <string_view>
#include <string>
#include <filesystem>

namespace ast
{
	[[nodiscard]] inline std::string sanitise_path(std::string_view path)
	{
		auto inputPath = std::filesystem::path(path);
		inputPath.make_preferred();

		if (inputPath.is_absolute())
			return inputPath.string();

		return (std::filesystem::current_path() / inputPath).lexically_normal().string();
	}
}

#pragma once
#include <string_view>
#include <string>
#include <filesystem>

namespace ast
{
	[[nodiscard]] inline std::string sanitise_path(std::string_view path, bool makePreferred = true)
	{
		auto inputPath = std::filesystem::path(path);

		if (makePreferred)
			inputPath.make_preferred();

		if (inputPath.is_absolute())
			return inputPath.string();

		return (std::filesystem::current_path() / inputPath).lexically_normal().string();
	}

	[[nodiscard]] inline std::string sanitise_path(const std::string& path, bool makePreferred = true)
	{
		return sanitise_path(std::string_view(path.c_str(), path.size()), makePreferred);
	}
}

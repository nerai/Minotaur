#pragma once

#include <chrono>
#include <cstdio>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>
#include <array>

#include "Global.h"



// ---------------------------------------------------------------------
// Util
// ---------------------------------------------------------------------

inline
double timeFromD(std::chrono::steady_clock::time_point t0) {
	auto t1 = std::chrono::steady_clock::now();
	auto dt = std::chrono::duration<double>(t1 - t0);
	return dt.count();
}

inline
std::string timeFromS(std::chrono::steady_clock::time_point t0) {
	auto dt = timeFromD(t0);
	return std::to_string(dt) + "s";
}

#ifdef __linux__
inline
std::string system_exec(const char* cmd) {
	std::unique_ptr<FILE, decltype(&pclose)> pipe(popen(cmd, "r"), pclose);
	if (!pipe) {
		return "ERROR: popen() failed";
	}

	std::array<char, 128> buffer;
	std::string result;
	while (fgets(buffer.data(), buffer.size(), pipe.get()) != nullptr) {
		result += buffer.data();
	}
	return result;
}
#endif

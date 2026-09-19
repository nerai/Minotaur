#pragma once



/*
* _ASSERT
*/
#if !defined(_MSC_VER)

#if DEBUG
#include "assert.h"
#ifndef _ASSERT
#define _ASSERT assert
#endif // !_ASSERT

#else
#define _ASSERT(x) void(0)
#endif

#endif


/*
* __assume
*/
#if defined(_MSC_VER)
// keep as-is
#elif defined(__GNUC__)
#define __assume( condition ) \
{ if(!(condition)) __builtin_unreachable(); }
#elif defined(__clang__)
#define __assume __builtin_assume
#endif



/*
* __forceinline
*/
#if defined(_MSC_VER)
// keep as-is
#else
#define __forceinline __attribute__((always_inline))
#endif

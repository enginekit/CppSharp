#include <string>

template std::allocator<char>::allocator();
template std::__cxx11::basic_string<char, std::char_traits<char>, std::allocator<char>>::basic_string(const char*, const std::allocator<char>&);
template const char* std::__cxx11::basic_string<char, std::char_traits<char>, std::allocator<char>>::c_str() const noexcept;

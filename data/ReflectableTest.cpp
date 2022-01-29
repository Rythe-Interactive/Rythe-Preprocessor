// ReflectableTest.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
namespace legion
{

    struct reflectable {};

}

struct [[legion::reflectable]] example_comp
{
    int value;
    bool b;
};

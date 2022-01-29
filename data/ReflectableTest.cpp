#include <string>
namespace legion
{
    struct reflectable {};
}

struct [[legion::reflectable]] example_comp
{
public:
    int value;
    bool b;
private:
    float f_value;
    std::string str;
public:
    void DoThing() {}
    void DoAnotherThing(int withThis) {}
};

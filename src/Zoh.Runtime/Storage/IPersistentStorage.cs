using Zoh.Runtime.Types;

namespace Zoh.Runtime.Storage;

public interface IPersistentStorage
{
    void Write(string? store, string varName, ZohValue value);
    ZohValue? Read(string? store, string varName);
    void Erase(string? store, string varName);
    void Purge(string? store);
    bool Exists(string? store, string varName);
}

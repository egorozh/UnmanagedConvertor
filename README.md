# UnmanagedConvertor
 Getting a native dll from .net

Use:

```c#
[Obfuscation(Feature = "DllExport")]
public static bool ExportedFunction(string id)
{
  return true;
}
```

Edit Post-build Event Command Line: 

C:\UnmanagedConvertor\UnmanagedConvertor.exe $(TargetPath)


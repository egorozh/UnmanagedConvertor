using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UnmanagedBuilder
{
    public class UnmanagedBuilder
    {
        public static void Build(string dllPath)
        {
            if (!File.Exists(dllPath))
                return;

            try
            {
                var fileInfo = new FileInfo(dllPath);

                var res = GetIlasmAndIldasmPaths(out var pathToIlasm, out var pathToIldasm);

                if (res)
                {
                    var fullName = fileInfo.FullName;

                    var path = DisasmDll(fullName, fileInfo, pathToIldasm);

                    WriteSpecificUnmanagedCode(path);

                    CompileDll(pathToIlasm, path);
                    File.Delete(path);
                    File.Delete(path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) + ".res");
                    foreach (var file in new DirectoryInfo(fileInfo.DirectoryName).GetFiles("*.resources"))
                        File.Delete(file.FullName);
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }
        }

        private static void CompileDll(string pathToIlasm, string path)
        {
            var arguments = " /DLL /OPTIMIZE /RESOURCE=\"" +
                            path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) +
                            ".res\" \"" +
                            path + "\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(pathToIlasm, arguments)
            };
            process.Start();
            process.WaitForExit();
        }

        private static void WriteSpecificUnmanagedCode(string path)
        {
            var builder = new StringBuilder(File.ReadAllText(path));

            var builder2 = builder.Replace(".corflags 0x00000001", ".corflags 0x00000002");

            var num1 = 0;
            int num2;
            for (var startIndex = builder2.ToString()
                    .IndexOf("System.Reflection.ObfuscationAttribute", 0, StringComparison.Ordinal);
                startIndex != -1;
                startIndex = builder2.ToString()
                    .IndexOf("System.Reflection.ObfuscationAttribute", num2, StringComparison.Ordinal))
            {
                ++num1;
                num2 = builder2.ToString().IndexOf("// llExport\r\n", startIndex, StringComparison.Ordinal) + 13;
                builder2 = builder2.Insert(num2, "    .export[" + num1 + "]\r\n");
            }

            var bytes = Encoding.UTF8.GetBytes(builder2.ToString());
            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                fileStream.WriteByte(239);
                fileStream.WriteByte(187);
                fileStream.WriteByte(191);
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static string DisasmDll(string fullName, FileInfo fileInfo, string pathToIldasm)
        {
            var path = fullName.Substring(0, fullName.LastIndexOf(".", StringComparison.Ordinal)) + ".il";
            var arguments = " /utf8 /OUT=\"" + path + "\" \"" + fileInfo.FullName + "\"";
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo(pathToIldasm, arguments)
            };
            process.Start();
            process.WaitForExit();
            return path;
        }

        private static bool GetIlasmAndIldasmPaths(out string pathToIlasm, out string pathToIldasm)
        {
            pathToIlasm = ConfigurationManager.AppSettings.Get("ilasmpath");
            pathToIldasm = ConfigurationManager.AppSettings.Get("ildasmpath");

            if (!File.Exists(pathToIlasm))
            {
                WriteLog("Путь к ассемблеру неверен!");
                return false;
            }

            if (!File.Exists(pathToIldasm))
            {
                WriteLog("Путь к дизассемблеру неверен!");
                return false;
            }

            return true;
        }

        private static void WriteLog(string message)
        {
            var streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\Error.txt");
            streamWriter.WriteLine(message);
            streamWriter.Close();
        }
    }
}
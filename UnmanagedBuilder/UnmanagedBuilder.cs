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

            var result = new Result<string> {Value = dllPath}
                .Bind(DisasmDll)
                .Bind(WriteSpecificUnmanagedCode)
                .Bind(CompileDll)
                .Bind(DeleteFiles);

            if (!result.IsSuccess)
                WriteLog(result.Error.Message);
        }
        
        private static Result<string> GetIldasmPath(Result<object> prevResult)
        {
            var result = new Result<string>();

            var pathToIldasm = ConfigurationManager.AppSettings.Get("ildasmpath");

            if (!File.Exists(pathToIldasm))
                result.Error = new Exception("Путь к дизассемблеру неверен!");

            result.Value = pathToIldasm;

            return result;
        }

        private static Result<string> DisasmDll(Result<string> dllPathResult)
        {
            var ilFileNameResult = GetIlFileName(dllPathResult);

            var arguments = " /utf8 /OUT=\"" + ilFileNameResult.Value + "\" \"" + dllPathResult.Value + "\"";

            var pathToIldasm = GetIldasmPath(new Result<object>()).Value;

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo(pathToIldasm, arguments)
            };
            process.Start();
            process.WaitForExit();

            return ilFileNameResult;
        }

        private static Result<string> GetIlFileName(Result<string> dllPathResult)
        {
            var result = new Result<string>();

            var dllPath = dllPathResult.Value;
            result.Value = dllPath.Substring(0, dllPath.LastIndexOf(".", StringComparison.Ordinal)) + ".il";

            return result;
        }

        private static Result<string> WriteSpecificUnmanagedCode(Result<string> ilFileNameResult)
        {
            var builder = new StringBuilder(File.ReadAllText(ilFileNameResult.Value));

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
            using (var fileStream = new FileStream(ilFileNameResult.Value, FileMode.Create))
            {
                fileStream.WriteByte(239);
                fileStream.WriteByte(187);
                fileStream.WriteByte(191);
                fileStream.Write(bytes, 0, bytes.Length);
            }

            return ilFileNameResult;
        }

        private static Result<string> GetIlasmPath(Result<object> prevResult)
        {
            var result = new Result<string>();

            var pathToIlasm = ConfigurationManager.AppSettings.Get("ilasmpath");

            if (!File.Exists(pathToIlasm))
                result.Error = new Exception("Путь к ассемблеру неверен!");

            result.Value = pathToIlasm;

            return result;
        }

        private static Result<string> CompileDll(Result<string> ilFileNameResult)
        {
            var path = ilFileNameResult.Value;

            var arguments = " /DLL /OPTIMIZE /RESOURCE=\"" +
                            path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) +
                            ".res\" \"" +
                            path + "\"";

            var pathToIlasm = GetIlasmPath(new Result<object>()).Value;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(pathToIlasm, arguments)
            };
            process.Start();
            process.WaitForExit();

            return ilFileNameResult;
        }

        private static Result<string> DeleteFiles(Result<string> ilFileNameResult)
        {
            var path = ilFileNameResult.Value;
            var fileInfo = new FileInfo(path);

            var directory = new DirectoryInfo(fileInfo.DirectoryName);


            File.Delete(path);
            File.Delete(path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) + ".res");
            foreach (var file in directory.GetFiles("*.resources"))
                File.Delete(file.FullName);

            return ilFileNameResult;
        }

        private static void WriteLog(string message)
        {
            var streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\Error.txt");
            streamWriter.WriteLine(message);
            streamWriter.Close();
        }
    }

    public class Result<T>
    {
        public T Value { get; set; }

        public Exception Error { get; set; }

        public bool IsSuccess => Error == null;
    }

    public static class Extensions
    {
        public static Result<R> Bind<T, R>(this Result<T> input, Func<Result<T>, Result<R>> nextFunction)
            where R : class
        {
            return input.IsSuccess
                ? nextFunction(input)
                : new Result<R> {Error = input.Error};
        }
    }
}
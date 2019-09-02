using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UnmanagedBuilder
{
    public class UnmanagedBuilder
    {
        /// <summary>
        /// Получение dll c экспортируемыми функциями
        /// </summary>
        /// <param name="dllPath">Путь к dll</param>
        public static void Build(string dllPath)
        {
            var result = ValidationPath(dllPath)
                .Bind(DisasmDll)
                .Bind(WriteSpecificUnmanagedCode)
                .Bind(CompileDll)
                .Bind(DeleteFiles);

            if (!result.IsSuccess)
                WriteLog(result.Error.Message);
        }

        #region Private Methods

        /// <summary>
        /// Проверка валидности пути к библиотеке
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static Result<string> ValidationPath(string path)
        {
            var result = new Result<string>();

            if (!File.Exists(path))
                result.Error = new Exception("Файла по данному пути не существует");
            else if (!path.EndsWith(".dll"))
                result.Error = new Exception("Файл имеет расширение отличное от .\"dll\"");
            else
                result.Value = path;

            return result;
        }

        /// <summary>
        /// Дизассемблирование сборки
        /// </summary>
        /// <param name="dllPathResult">Путь к dll</param>
        /// <returns></returns>
        private static Result<string> DisasmDll(Result<string> dllPathResult)
        {
            var ilFileNameResult = GetIlFileName(dllPathResult);

            if (ilFileNameResult.IsSuccess)
            {
                var arguments = " /utf8 /OUT=\"" + ilFileNameResult.Value + "\" \"" + dllPathResult.Value + "\"";

                var pathToIldasm = GetIldasmPath(new Result<object>());

                if (pathToIldasm.IsSuccess)
                {
                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo(pathToIldasm.Value, arguments)
                        };
                        process.Start();
                        process.WaitForExit();
                    }
                    catch (Exception exception)
                    {
                        ilFileNameResult.Error = exception;
                    }
                }
                else
                {
                    ilFileNameResult.Error = pathToIldasm.Error;
                }
            }

            return ilFileNameResult;
        }

        private static Result<string> GetIlFileName(Result<string> dllPathResult)
        {
            var result = new Result<string>();

            try
            {
                var dllPath = dllPathResult.Value;
                result.Value = dllPath.Substring(0, dllPath.LastIndexOf(".", StringComparison.Ordinal)) + ".il";
            }
            catch (Exception exception)
            {
                result.Error = exception;
            }

            return result;
        }

        private static Result<string> GetIldasmPath(Result<object> prevResult)
        {
            var result = new Result<string>();

            var pathToIldasm = ConfigurationManager.AppSettings.Get("ildasmpath");

            if (!File.Exists(pathToIldasm))
                result.Error = new Exception("ildasm.exe по данному пути отсутствует");
            if (!pathToIldasm.EndsWith("ildasm.exe"))
                result.Error = new Exception("Путь к дизассемблеру неверен!");
            else
                result.Value = pathToIldasm;

            return result;
        }

        /// <summary>
        /// Изменяем IL код для экспорта функций, помеченных атрибутом <see cref="System.Reflection.ObfuscationAttribute"/>
        /// </summary>
        /// <param name="ilFileNameResult">Путь к il-файлу</param>
        /// <returns></returns>
        private static Result<string> WriteSpecificUnmanagedCode(Result<string> ilFileNameResult)
        {
            try
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
            }
            catch (Exception exception)
            {
                ilFileNameResult.Error = exception;
            }

            return ilFileNameResult;
        }

        /// <summary>
        /// Компилируем измененный il-код в сборку
        /// </summary>
        /// <param name="ilFileNameResult">Путь к il-файлу</param>
        /// <returns></returns>
        private static Result<string> CompileDll(Result<string> ilFileNameResult)
        {
            var path = ilFileNameResult.Value;

            var arguments = " /DLL /OPTIMIZE /RESOURCE=\"" +
                            path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) +
                            ".res\" \"" +
                            path + "\"";

            var pathToIlasm = GetIlasmPath(new Result<object>());

            if (pathToIlasm.IsSuccess)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo(pathToIlasm.Value, arguments)
                    };
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception exception)
                {
                    ilFileNameResult.Error = exception;
                }
            }
            else
            {
                ilFileNameResult.Error = pathToIlasm.Error;
            }

            return ilFileNameResult;
        }

        private static Result<string> GetIlasmPath(Result<object> prevResult)
        {
            var result = new Result<string>();

            var pathToIlasm = ConfigurationManager.AppSettings.Get("ilasmpath");

            if (!File.Exists(pathToIlasm))
                result.Error = new Exception("ilasm.exe по данному пути отсутствует");
            if (!pathToIlasm.EndsWith("ilasm.exe"))
                result.Error = new Exception("Путь к ассемблеру неверен!");
            else
                result.Value = pathToIlasm;

            return result;
        }

        /// <summary>
        /// Удаляем промежуточные файлы
        /// </summary>
        /// <param name="ilFileNameResult">Путь к il-файлу</param>
        /// <returns></returns>
        private static Result<string> DeleteFiles(Result<string> ilFileNameResult)
        {
            var path = ilFileNameResult.Value;

            try
            {
                var fileInfo = new FileInfo(path);

                var directory = new DirectoryInfo(fileInfo.DirectoryName);
                
                File.Delete(path);
                File.Delete(path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) + ".res");
                foreach (var file in directory.GetFiles("*.resources"))
                    File.Delete(file.FullName);
            }
            catch (Exception exception)
            {
                ilFileNameResult.Error = exception;
            }

            return ilFileNameResult;
        }

        /// <summary>
        /// Записываем сообщение в лог
        /// </summary>
        /// <param name="message"></param>
        private static void WriteLog(string message)
        {
            try
            {
                var streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\Error.txt");
                streamWriter.WriteLine(message);
                streamWriter.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        #endregion
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
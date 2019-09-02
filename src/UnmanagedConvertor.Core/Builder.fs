namespace UnmanagedConvertor.Core

open System;
open System.Configuration;
open System.Diagnostics;
open System.IO;
open System.Text;
  
module UnmanagedBuilder =
    let Build dllPath =
        
        let writeLog (message : string) =
            let streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\Error.txt")
            streamWriter.WriteLine message
            streamWriter.Close()
        
        let getIlasmAndIldasmPaths =        
            let pathToIlasm = ConfigurationManager.AppSettings.Get "ilasmpath"
            let pathToIldasm = ConfigurationManager.AppSettings.Get "ildasmpath"
            if (not (File.Exists pathToIlasm)) then        
                writeLog "Путь к ассемблеру неверен!"        
            if (not (File.Exists pathToIldasm)) then         
                writeLog "Путь к дизассемблеру неверен!"                
            
            (pathToIlasm,pathToIldasm)
        
        let disasmDll (fullName : string,  fileInfo:FileInfo, pathToIldasm :string) =  
            let i = fullName.LastIndexOf (".", StringComparison.Ordinal)
            let s = fullName.Substring (0, i)
            let path = s+ ".il"
            let arguments = " /utf8 /OUT=\"" + path + "\" \"" + fileInfo.FullName + "\""
            let proc = new Process()
            proc.StartInfo <- new ProcessStartInfo(pathToIldasm, arguments)           
            proc.Start() |> ignore
            proc.WaitForExit()
            path
        
        let writeSpecificUnmanagedCode (path: string) =    
            let builder = new StringBuilder(File.ReadAllText path)

            builder.Replace (".corflags 0x00000001", ".corflags 0x00000002") |> ignore

            let mutable num1 = 0
            let mutable num2 = 0        

            let mutable i = builder.ToString().IndexOf("System.Reflection.ObfuscationAttribute", 0, StringComparison.Ordinal);

            let func i =
                num1 <- num1 + 1
                num2 <- builder.ToString().IndexOf("// llExport\r\n", i, StringComparison.Ordinal) + 13
                builder = builder.Insert(num2, "    .export[" + num1.ToString() + "]\r\n")


            while (not (i.Equals -1)) do
                func i |> ignore
                i <- builder.ToString().IndexOf("System.Reflection.ObfuscationAttribute", num2, StringComparison.Ordinal)                                        

            let bytes = builder.ToString() |> Encoding.UTF8.GetBytes
            let fileStream = new FileStream(path, FileMode.Create)
           
            fileStream.WriteByte 239uy
            fileStream.WriteByte 187uy
            fileStream.WriteByte 191uy
            fileStream.Write (bytes, 0 , bytes.Length)
            fileStream.Close()
                    
        let compileDll (pathToIlasm : string, path : string) =       
            let arguments = " /DLL /OPTIMIZE /RESOURCE=\"" +
                            path.Substring(0, path.LastIndexOf(".", StringComparison.Ordinal)) +
                            ".res\" \"" +
                            path + "\"";

            let proc = new Process()
            proc.StartInfo <- new ProcessStartInfo(pathToIlasm, arguments)            
            proc.Start() |> ignore
            proc.WaitForExit()                     
                         
        let main dllPath =
            let fileInfo = new FileInfo(dllPath)
            let (pathToIlasm,pathToIldasm) = getIlasmAndIldasmPaths
            
            let fullName = fileInfo.FullName
            let path = disasmDll (fullName, fileInfo, pathToIldasm)
            writeSpecificUnmanagedCode path
            
            compileDll (pathToIlasm, path)
            File.Delete path
            let i =path.LastIndexOf (".", StringComparison.Ordinal)
            let s = path.Substring(0, i)
            
            File.Delete (s + ".res")
            
            let dir = new DirectoryInfo(fileInfo.DirectoryName)
            dir.GetFiles "*.resources" 
            |> Array.iter (fun file -> (File.Delete file.FullName))
        
        if (not (File.Exists dllPath)) then (writeLog "None")
        else          
            try
                main dllPath
            with
                |_ as ex -> writeLog ex.Message   
               
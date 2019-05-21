namespace UnmanagedConvertor
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 1)
                UnmanagedBuilder.UnmanagedBuilder.Build(args[0]);
        }
    }
}
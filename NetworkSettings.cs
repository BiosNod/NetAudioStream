namespace StreamingApplication
{
    public static class NetworkSettings
    {
        public static (string ip, int port) GetServerSettings()
        {
            const string ipDefault = "0.0.0.0";
            Console.Write($"Enter IP [{ipDefault}]: ");
            var ipInput = Console.ReadLine();

            // Исправленная проверка
            var ip = string.IsNullOrWhiteSpace(ipInput)
                ? ipDefault
                : ipInput.Trim();

            const int portDefault = 49800;
            Console.Write($"Enter Port [{portDefault}]: ");
            var portInput = Console.ReadLine();

            int port = string.IsNullOrWhiteSpace(portInput)
                ? portDefault
                : int.Parse(portInput);

            return (ip, port);
        }

        public static (string ip, int port) GetClientSettings()
        {
            const string ipDefault = "127.0.0.1";
            Console.Write($"Enter Server IP [{ipDefault}]: ");
            var ipInput = Console.ReadLine();

            var ip = string.IsNullOrWhiteSpace(ipInput)
                ? ipDefault
                : ipInput.Trim();

            const int portDefault = 49800;
            Console.Write($"Enter Server Port [{portDefault}]: ");
            var portInput = Console.ReadLine();

            int port = string.IsNullOrWhiteSpace(portInput)
                ? portDefault
                : int.Parse(portInput);

            return (ip, port);
        }
    }
}
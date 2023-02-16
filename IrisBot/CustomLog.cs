using Discord;

namespace IrisBot
{
    public class CustomLog
    {
        private static object _MessageLock = new object(); // ThreadSafe 상태로 color를 변경하기 위함

        /// <summary>
        /// 로그를 출력하는 함수
        /// </summary>
        /// <param name="logLevel">로그 레벨</param>
        /// <param name="source">로그 종류</param>
        /// <param name="text">내용</param>
        public static void PrintLog(LogSeverity logLevel, string source, string text)
        {
            lock (_MessageLock) // ThreadSafe 상태로 color를 변경하기 위함
            {
                Console.Write(DateTime.Now.ToString("hh:mm:ss"));
                switch (logLevel)
                {
                    case LogSeverity.Critical:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(" [CRITICAL] ");
                        break;
                    case LogSeverity.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(" [ERROR] ");
                        break;
                    case LogSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(" [WARN] ");
                        break;
                    case LogSeverity.Info:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(" [INFO] ");
                        break;
                    case LogSeverity.Verbose:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(" [VERBOSE] ");
                        break;
                    case LogSeverity.Debug:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(" [DEBUG] ");
                        break;
                }
                Console.ResetColor();
                Console.Write($"{source}\r\t\t\t\t{text}{Environment.NewLine}");
            }
        }

        /// <summary>
        /// Exception 처리 함수. 콘솔 및 로그 파일로 예외를 출력한다.
        /// </summary>
        /// <param name="ex">Exception</param>
        public async static Task ExceptionHandler(Exception ex)
        {
            try
            {
                lock (_MessageLock) // ThreadSafe 상태로 color를 변경하기 위함
                {
                    Console.Write(DateTime.Now.ToString("hh:mm:ss"));
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write(" [EXCEPTION] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Exception occured. See detailed message below.\r\n");
                    Console.WriteLine(ex.ToString());
                    Console.ResetColor();
                }

                string ExceptionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exception");
                string FileName = $"[{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}]_Exception.log"; // ..\Exception\[2023-02-16-13-51-40]_Exception.log
                if (!Directory.Exists(ExceptionDirectory))
                    Directory.CreateDirectory(ExceptionDirectory);
                
                using (StreamWriter sw = new StreamWriter(Path.Combine(ExceptionDirectory, FileName)))
                {
                    await sw.WriteLineAsync(ex.ToString());
                }
            }
            catch
            {
                
            }
        }
    }
}

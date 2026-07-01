//异步编程允许程序在等待长时间操作（如文件IO、网络请求）完成时，继续执行其他任务，而不是阻塞当前线程。
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace name
{
    class TaskClass
    {
        public async Task<int> CalculateAsync()
        {
            Console.WriteLine($"  [CalculateAsync] 开始执行");
            //await表示让当前线程继续往后执行，当前的任务先阻塞，等待dylay延迟完成后，继续回来执行，
            // 类似于微波炉定时器，定时结束了才回来取餐，定时期间去做其他事（但回来拿餐的人不一定是之前的人，线程不一定是之前的线程）
            await Task.Delay(1000);
            Console.WriteLine($"  [CalculateAsync] 执行完成");
            return 42;
        }
    }
    
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine($"[Main] 程序开始");
            
            var calculator = new TaskClass();
            
            // 启动异步操作，但不等待
            Console.WriteLine($"[Main] 启动 CalculateAsync...");
            Task<int> task = calculator.CalculateAsync();
            
            // 检查任务状态，status
            Console.WriteLine($"[Main] 任务状态: {task.Status}");
            
            // 主线程可以做其他事情
            Console.WriteLine($"[Main] 主线程继续工作...");
            await Task.Delay(2000);  // 模拟工作2秒
            
            Console.WriteLine($"等待任务结果...");
            
            // 等待任务完成并获取结果
            int result = await task;
            
            Console.WriteLine($"[Main] 结果: {result}");
        }
    }
}
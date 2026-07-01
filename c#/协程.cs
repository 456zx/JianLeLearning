//协程是一种用户态的轻量级线程，由程序员控制其调度。协程的执行是协作式而非抢占式的，即同一时间只能有一个协程在执行
using System;
using System.Collections;
namespace Name
{
    class IEnumeratorClass
    {
        public IEnumerator Show()
        {
            for(int i = 0; i < 10; i++)
            {
                //当使用yield return时，编译器会自动生成一个实现了 IEnumerator 的状态机类
                //current可以访问当前状态，
                // MoveNext可以移动到下一个元素，
                // Reset可以重置到初始位置
                yield return i;
            }
        }
    }
    class Result
    {
        static void Main(string[]args)
        {
            Name.IEnumeratorClass a = new Name.IEnumeratorClass();
            //获取ienumerator对象
            IEnumerator enumerator = a.Show();
            //通过movenext来获取下一个enumerator的值
            while (enumerator.MoveNext())
            {
                //用enumerator.current来访问当前值
                Console.WriteLine("show:{0}",enumerator.Current);
            }
        }
    }
}
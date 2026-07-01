//unitask类似于c#异步async的用法
//标准 Task：每次调用都分配内存，
//unitask无GC分配
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
class AllocationComparison : MonoBehaviour
{
     public async Task StandardMethod()
    {
        await Task.Delay(1000);  // 分配 Task 对象
        Debug.Log("完成");
    }

    public async UniTask UnityMethod()
    {
        await UniTask.Delay(1000);  // 无分配，结构体，在栈上分配，函数返回自动释放
        Debug.Log("完成");
    }
    public static async UniTask Main(string[]args)
    {
        AllocationComparison a = new AllocationComparison();
        Console.WriteLine("{0}",a.UnityMethod());
    }
}

//为什么结构体比类的开销更小
// 堆分配步骤：
// 1. 计算内存大小
// 2. 在堆上找空闲空间
// 3. 更新堆指针
// 4. 可能有GC压力

// 栈分配步骤：
// 1. 直接移动栈指针
// 几乎零开销
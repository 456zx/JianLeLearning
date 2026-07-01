using System;
class Types
{
    //当一个值类型转换为对象类型时，则被称为 装箱
    public object obj = 100;
    //这些变量的类型检查是在运行时发生的
    public dynamic value = 1.1;
    //字符串类型 允许您给变量分配任何字符串值
    public string str1 = "Hello1";
    //字符串（String）类型的值可以通过两种形式进行分配：引号和 @引号
    public string str2 = @"Hello1";

    //注：静态方法不能直接访问类成员，所以可以将字段设为静态或创建实例
    static public void Main(string[] args)
    {
        //c#中只能通过new创建实例，否则实例为null
        Types t = new Types();
        Console.WriteLine("str1:{0}",t.str1);
        Console.WriteLine("str2:{0}",t.str2);
    }
}

    

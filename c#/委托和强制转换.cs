using System;
//定义一个委托叫numberchanger
delegate int NumberChanger(int n,double m);
namespace DelegateTest
{
    class DelegateTest
    {
        static int num = 9;
        public static int AddNum(int n,double m)
        {
            //强制转换，统一为类型int
            num = num + n + (int)m;
            return num;
        }
        public static int MultNum(int n)
        {
            num *= n;
            return num;
        }
        public static int getNum()
        {
            return num;
        }
        static void Main(string[] args)
        {
            //创建委托实例
            NumberChanger NC1 = new NumberChanger(AddNum);
            //NumberChanger NC2 = new NumberChanger(MultNum);
            //使用委托调用方法
            NC1(1,2);
            Console.WriteLine("Value of Num: {0}", getNum());
            // NC2(2);
            // Console.WriteLine("Value of Num: {0}", getNum());
        }
    }
}
    
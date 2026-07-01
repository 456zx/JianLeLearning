using System;
using System.Drawing;
namespace RecApp
{
    class Rec
    {
        double length;
        double width;
        public void acceptdetial()
        {
            length = 4.5;
            width = 3.5;
        }
        public double GetArea()
        {
            return length*width;
        }
        public void Display()
        {
            Console.WriteLine("Length:{0}",length);
            Console.WriteLine("Length:{0}",width);
            Console.WriteLine("Area: {0}",GetArea());
        }
    }
    class Result
    {
        static void Main(string[] args)
        {
            Rec r = new Rec();
            r.acceptdetial();
            r.Display();
            Console.ReadLine();
        }
    }
}
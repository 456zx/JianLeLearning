using System;
using System.Collections.Generic;

namespace DesignPatternExamples
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("===== 观察者模式 =====");
            ObserverPatternDemo.Run();

            Console.WriteLine();
            Console.WriteLine("===== 工厂模式 =====");
            FactoryPatternDemo.Run();

            Console.WriteLine();
            Console.WriteLine("===== 策略模式 =====");
            StrategyPatternDemo.Run();

            Console.WriteLine();
            Console.WriteLine("===== 对象池模式 =====");
            ObjectPoolPatternDemo.Run();

            Console.WriteLine();
            Console.WriteLine("===== ECS 模式 =====");
            ECSPatternDemo.Run();
        }
    }

    // --------------------------------------------------
    // 1. 观察者模式
    // 核心思想：
    // 一个对象状态变化时，自动通知所有依赖它的对象。
    // --------------------------------------------------

    // 观察者接口：所有观察者都要实现 Update 方法
    interface IObserver
    {
        void Update(string message);
    } 

    // 被观察者接口：提供订阅、取消订阅、通知的能力
    interface ISubject
    {
        void Attach(IObserver observer);
        void Detach(IObserver observer);
        void Notify(string message);
    }

    // 具体被观察者：这里用“主播”举例
    class Streamer : ISubject
    {
        private readonly List<IObserver> observers = new List<IObserver>();

        public void Attach(IObserver observer)
        {
            observers.Add(observer);
        }

        public void Detach(IObserver observer)
        {
            observers.Remove(observer);
        }

        public void Notify(string message)
        {
            foreach (IObserver observer in observers)
            {
                observer.Update(message);
            }
        }

        public void StartLive()
        {
            Console.WriteLine("主播开播了。");
            Notify("你关注的主播已经开播。");
        }
    }

    // 具体观察者：粉丝
    class Fan : IObserver
    {
        private readonly string name;

        public Fan(string name)
        {
            this.name = name;
        }

        public void Update(string message)
        {
            Console.WriteLine(name + " 收到通知：" + message);
        }
    }

    class ObserverPatternDemo
    {
        public static void Run()
        {
            Streamer streamer = new Streamer();

            Fan fanA = new Fan("小明");
            Fan fanB = new Fan("小红");

            streamer.Attach(fanA);
            streamer.Attach(fanB);

            // 当主播状态变化时，所有订阅者都会收到消息
            streamer.StartLive();
        }
    }

    // --------------------------------------------------
    // 2. 工厂模式
    // 核心思想：
    // 不直接 new 具体对象，而是通过工厂统一创建。
    // 这样可以隐藏创建细节，降低调用方和具体类的耦合。
    // --------------------------------------------------

    // 产品接口：不同类型的角色都实现同一个接口
    interface ICharacter
    {
        void Attack();
    }

    class Warrior : ICharacter
    {
        public void Attack()
        {
            Console.WriteLine("战士使用大剑攻击。");
        }
    }

    class Mage : ICharacter
    {
        public void Attack()
        {
            Console.WriteLine("法师释放火球术。");
        }
    }

    // 工厂类：根据传入参数决定创建哪种具体对象
    class CharacterFactory
    {
        public static ICharacter CreateCharacter(string type)
        {
            if (type == "Warrior")
            {
                return new Warrior();
            }

            if (type == "Mage")
            {
                return new Mage();
            }

            throw new ArgumentException("未知角色类型：" + type);
        }
    }

    class FactoryPatternDemo
    {
        public static void Run()
        {
            ICharacter warrior = CharacterFactory.CreateCharacter("Warrior");
            ICharacter mage = CharacterFactory.CreateCharacter("Mage");

            warrior.Attack();
            mage.Attack();
        }
    }

    // --------------------------------------------------
    // 3. 策略模式
    // 核心思想：
    // 把“算法”或“行为”单独封装起来，并且可以在运行时自由切换。
    // --------------------------------------------------

    // 策略接口：定义统一的计算规则
    interface IDiscountStrategy
    {
        double CalculatePrice(double originalPrice);
    }

    // 普通用户：不打折
    class NormalDiscount : IDiscountStrategy
    {
        public double CalculatePrice(double originalPrice)
        {
            return originalPrice;
        }
    }

    // 会员用户：打 8 折
    class MemberDiscount : IDiscountStrategy
    {
        public double CalculatePrice(double originalPrice)
        {
            return originalPrice * 0.8;
        }
    }

    // VIP 用户：打 6 折
    class VipDiscount : IDiscountStrategy
    {
        public double CalculatePrice(double originalPrice)
        {
            return originalPrice * 0.6;
        }
    }

    // 上下文类：持有一个策略对象，真正计算时交给策略处理
    class PriceContext
    {
        private IDiscountStrategy strategy;

        public PriceContext(IDiscountStrategy strategy)
        {
            this.strategy = strategy;
        }

        public void SetStrategy(IDiscountStrategy strategy)
        {
            this.strategy = strategy;
        }

        public double GetFinalPrice(double originalPrice)
        {
            return strategy.CalculatePrice(originalPrice);
        }
    }

    class StrategyPatternDemo
    {
        public static void Run()
        {
            double price = 100;

            PriceContext context = new PriceContext(new NormalDiscount());
            Console.WriteLine("普通用户价格：" + context.GetFinalPrice(price));

            context.SetStrategy(new MemberDiscount());
            Console.WriteLine("会员用户价格：" + context.GetFinalPrice(price));

            context.SetStrategy(new VipDiscount());
            Console.WriteLine("VIP 用户价格：" + context.GetFinalPrice(price));
        }
    }

    // --------------------------------------------------
    // 4. 对象池模式
    // 核心思想：
    // 提前创建一批可复用对象，需要时取出，用完后归还。
    // 常用于频繁创建/销毁对象的场景，比如子弹、特效、数据库连接。
    // --------------------------------------------------

    // 池中对象：这里用“子弹”举例
    class Bullet
    {
        public int Id { get; private set; }
        public bool IsActive { get; set; }

        public Bullet(int id)
        {
            Id = id;
            IsActive = false;
        }

        public void Use()
        {
            IsActive = true;
            Console.WriteLine("子弹 " + Id + " 被取出并开始使用。");
        }

        public void Reset()
        {
            IsActive = false;
            Console.WriteLine("子弹 " + Id + " 已归还对象池。");
        }
    }

    class BulletPool
    {
        private readonly Queue<Bullet> pool = new Queue<Bullet>();

        public BulletPool(int initialCount)
        {
            for (int i = 1; i <= initialCount; i++)
            {
                pool.Enqueue(new Bullet(i));
            }
        }

        public Bullet GetBullet()
        {
            if (pool.Count == 0)
            {
                Console.WriteLine("对象池为空，临时创建一颗新子弹。");
                return new Bullet(-1);
            }

            Bullet bullet = pool.Dequeue();
            bullet.Use();
            return bullet;
        }

        public void ReturnBullet(Bullet bullet)
        {
            bullet.Reset();
            pool.Enqueue(bullet);
        }
    }

    class ObjectPoolPatternDemo
    {
        public static void Run()
        {
            BulletPool bulletPool = new BulletPool(2);

            Bullet bullet1 = bulletPool.GetBullet();
            Bullet bullet2 = bulletPool.GetBullet();

            // 前两颗从池中取出后，先归还一颗
            bulletPool.ReturnBullet(bullet1);

            // 再次获取时，拿到的是归还后可复用的对象
            Bullet bullet3 = bulletPool.GetBullet();

            // 用完继续归还，方便下次复用
            bulletPool.ReturnBullet(bullet2);
            bulletPool.ReturnBullet(bullet3);
        }
    }

    // --------------------------------------------------
    // 5. ECS 模式
    // 核心思想：
    // Entity（实体）只负责标识身份
    // Component（组件）只负责存数据
    // System（系统）只负责逻辑处理
    // 这样能让数据和行为解耦，特别适合游戏开发。
    // --------------------------------------------------

    // 实体：本身不存复杂逻辑，只用 Id 表示是谁
    class Entity
    {
        public int Id { get; private set; }

        public Entity(int id)
        {
            Id = id;
        }
    }

    // 组件：只存位置数据
    class PositionComponent
    {
        public float X;
        public float Y;
    }

    // 组件：只存速度数据
    class VelocityComponent
    {
        public float SpeedX;
        public float SpeedY;
    }

    // 世界：统一管理实体和它们拥有的组件
    class World
    {
        private int nextEntityId = 1;

        public List<Entity> Entities { get; private set; }
        public Dictionary<int, PositionComponent> Positions { get; private set; }
        public Dictionary<int, VelocityComponent> Velocities { get; private set; }

        public World()
        {
            Entities = new List<Entity>();
            Positions = new Dictionary<int, PositionComponent>();
            Velocities = new Dictionary<int, VelocityComponent>();
        }

        public Entity CreateEntity()
        {
            Entity entity = new Entity(nextEntityId++);
            Entities.Add(entity);
            return entity;
        }
    }

    // 系统：只负责处理“拥有位置和速度的实体”的移动逻辑
    class MoveSystem
    {
        public void Update(World world)
        {
            foreach (Entity entity in world.Entities)
            {
                // 只有同时拥有 Position 和 Velocity 的实体才参与移动
                if (world.Positions.ContainsKey(entity.Id) && world.Velocities.ContainsKey(entity.Id))
                {
                    PositionComponent position = world.Positions[entity.Id];
                    VelocityComponent velocity = world.Velocities[entity.Id];

                    position.X += velocity.SpeedX;
                    position.Y += velocity.SpeedY;

                    Console.WriteLine("实体 " + entity.Id + " 移动后位置：(" + position.X + ", " + position.Y + ")");
                }
            }
        }
    }

    class ECSPatternDemo
    {
        public static void Run()
        {
            World world = new World();

            Entity player = world.CreateEntity();

            // 给实体添加“位置组件”
            world.Positions[player.Id] = new PositionComponent
            {
                X = 0,
                Y = 0
            };

            // 给实体添加“速度组件”
            world.Velocities[player.Id] = new VelocityComponent
            {
                SpeedX = 2,
                SpeedY = 1
            };

            MoveSystem moveSystem = new MoveSystem();
            moveSystem.Update(world);
        }
    }
}

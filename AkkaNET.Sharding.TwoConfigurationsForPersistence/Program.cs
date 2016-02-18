using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence.Sqlite;
using Akka.Persistence.SqlServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaNET.Sharding.TwoConfigurationsForPersistence
{
    class Program
    {
        static void Main(string[] args)
        {
            ActorSystem system = ActorSystem.Create("AkkaCluster");
            SqlitePersistence.Get(system);
            SqlServerPersistence.Get(system);

            IActorRef region = ClusterSharding.Get(system).Start(
                typeName: typeof(Worker).Name,
                entityProps: Props.Create<Worker>(),
                settings: ClusterShardingSettings.Create(system),
                messageExtractor: new Worker.Message.Extractor()
                );

            IActorRef bell = system.ActorOf(Props.Create<Bell>(region));
            Bell.RingTheBell ringTheBell = new Bell.RingTheBell();

            system.Scheduler.ScheduleTellRepeatedly(
                initialDelay: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromSeconds(1),
                receiver: bell,
                sender: ActorRefs.Nobody,
                message: ringTheBell
                );

            Console.WriteLine("App is working. Press any key to exit...");
            Console.ReadKey();

            Task.WaitAll(system.Terminate());

            Console.WriteLine("Exiting...");
        }
    }

    public class Bell: ReceiveActor
    {
        private IActorRef _shardRegion;
        private int _innerCounter;
        public Bell(IActorRef shardRegion)
        {
            _shardRegion = shardRegion;

            Receive<RingTheBell>(msg =>
            {
                int id = ++_innerCounter % 10;
                _shardRegion.Tell(new Worker.Message(id, _innerCounter));
            });
        }

        public class RingTheBell { }
    }
}

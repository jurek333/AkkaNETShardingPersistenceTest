using System;
using Akka.Cluster.Sharding;
using Akka.Persistence;
using Akka.Actor;

namespace AkkaNET.Sharding.TwoConfigurationsForPersistence
{
    public class Worker: ReceivePersistentActor
    {
        private const int _timeoutInSeconds = 60;
        public override string PersistenceId { get { return Self.Path.Name; } }

        private WorkerContent _content;

        public Worker() {
            _content = new WorkerContent();

            Recover<SnapshotOffer>(offer =>
            {
                var newContent = offer.Snapshot as WorkerContent;
                if (newContent != null)
                {
                    _content = newContent;
                }
            });
            Command<SaveSnapshotSuccess>(success => DeleteMessages(success.Metadata.SequenceNr, false));
            Command<SaveSnapshotFailure>(failure => { /* do nothing */ });
            Recover<WorkerEvent>(ev => Handle(ev));
            Command<Message>(msg => {
                WorkerEvent ev = new WorkerEvent(msg);
                Persist(ev, e => Handle(e));
            });

            Command<ReceiveTimeout>(msg =>
            {
                Context.Parent.Tell(new Passivate(new StopActor()));
            });
            Command<StopActor>(msg =>
            {
                Context.Stop(Self);
            });
            Context.SetReceiveTimeout(TimeSpan.FromSeconds(_timeoutInSeconds));
        }

        private void Handle(WorkerEvent ev)
        {
            _content = _content.Handle(ev);
            if (_content.TotalCountOfEvents % 3 == 0)
            {
                SaveSnapshot(_content);
            }
        }

        public class WorkerEvent
        {
            public DateTime WhenCreated { get; set; }
            public int MsgNumber { get; set; }

            public WorkerEvent() {
                WhenCreated = DateTime.UtcNow;
            }
            public WorkerEvent(Message msg)
            {
                WhenCreated = msg.WhenCreated;
                MsgNumber = msg.MsgNumber;
            }
        }

        public class StopActor {
            public DateTime WhenCreated { get; }

            public StopActor()
            {
                WhenCreated = DateTime.UtcNow;
            }
        }
        public class Message
        {
            public int Id { get; private set; }
            public DateTime WhenCreated { get; private set; }
            public int MsgNumber { get; private set; }

            public Message(int id) {
                WhenCreated = DateTime.UtcNow;
                Id = id;
            }

            public Message(int id, int num):this(id)
            {
                MsgNumber = num;
            }

            public class Extractor : IMessageExtractor
            {
                public string EntityId(object message)
                {
                    return (message as Worker.Message)?.Id.ToString();
                }

                public object EntityMessage(object message)
                {
                    return message;
                }

                public string ShardId(object message)
                {
                    return ((message as Worker.Message)?.Id % 10).ToString();
                }
            }
        }
    }

    public class WorkerContent
    {
        public int LastNumber { get; }
        public DateTime TimeOfLastEvent { get; }
        public int TotalCountOfEvents { get; }

        public WorkerContent()
        {
            TimeOfLastEvent = DateTime.UtcNow;
        }

        public WorkerContent(int num, int total):this() {
            LastNumber = num;
            TotalCountOfEvents = total;
        }

        public WorkerContent Handle(Worker.WorkerEvent ev)
        {
            return new WorkerContent(ev.MsgNumber, 1 + this.TotalCountOfEvents);
        }
    }
}
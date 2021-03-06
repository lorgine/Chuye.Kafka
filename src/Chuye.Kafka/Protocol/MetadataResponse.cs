﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chuye.Kafka.Serialization;

namespace Chuye.Kafka.Protocol {
    //MetadataResponse => [Broker][TopicMetadata]
    public partial class MetadataResponse : Response {
        public Broker[] Brokers { get; set; }
        public TopicMetadata[] TopicMetadatas { get; set; }

        protected override void DeserializeContent(KafkaReader reader) {
            Brokers = reader.ReadArray(() => new Broker(0, null, 0));
            //Brokers = reader.ReadArray<Broker>();
            TopicMetadatas = reader.ReadArray<TopicMetadata>();
        }

        protected override void SerializeContent(KafkaWriter writer) {
            writer.Write(Brokers);
            writer.Write(TopicMetadatas);
        }

        public override void TryThrowFirstErrorOccured() {
            var errors = TopicMetadatas.Select(x => x.TopicErrorCode)
                .Where(x => x != ErrorCode.NoError);
            if (errors.Any()) {
                throw new ProtocolException(errors.First());
            }
            errors = TopicMetadatas.SelectMany(x => x.PartitionMetadatas)
                .Select(x => x.PartitionErrorCode)
                .Where(x => x != ErrorCode.NoError);
            if (errors.Any()) {
                throw new ProtocolException(errors.First());
            }
        }


        public IEnumerable<Int32> FindPartitionByTopic(String topic) {
            return TopicMetadatas.Where(x => x.TopicName.Equals(topic, StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.PartitionMetadatas)
                .Select(x => x.PartitionId)
                .OrderBy(x => x);
        }

        public Broker FindBrokerByPartition(String topic, Int32 partition) {
            var meta = TopicMetadatas.Where(x => x.TopicName.Equals(topic, StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.PartitionMetadatas)
                .SingleOrDefault(x => x.PartitionId == partition);
            if (meta == null) {
                throw new ArgumentOutOfRangeException();
            }
            return Brokers.SingleOrDefault(r => r.NodeId == meta.Leader);
        }
    }

    //Broker => NodeId Host Port  (any number of brokers may be returned)
    //  NodeId => int32
    //  Host => string
    //  Port => int32
    public class Broker : IKafkaReadable, IKafkaWriteable, IEquatable<Broker> {
        public Int32 NodeId { get; private set; }
        public String Host { get; private set; }
        public Int32 Port { get; private set; }

        //internal Broker() {
        //}

        public Broker(Int32 nodeId, String host, Int32 port) {
            NodeId = nodeId;
            Host   = host;
            Port   = port;
        }

        public void FetchFrom(KafkaReader reader) {
            NodeId = reader.ReadInt32();
            Host   = reader.ReadString();
            Port   = reader.ReadInt32();
        }

        public void SaveTo(KafkaWriter writer) {
            writer.Write(NodeId);
            writer.Write(Host);
            writer.Write(Port);
        }

        public Uri ToUri() {
            return new Uri(String.Format("http://{0}:{1}", Host, Port));
        }

        public bool Equals(Broker other) {
            return other != null
                && NodeId == other.NodeId
                && String.Equals(Host, other.Host)
                && Port == other.Port;
        }

        public override bool Equals(object obj) {
            return base.Equals(obj as Broker);
        }

        public override int GetHashCode() {
            int hash = 17;
            unchecked {
                hash = hash * 23 ^ NodeId;
                hash = hash * 23 ^ Host.GetHashCode();
                hash = hash * 23 ^ Port;
            }
            return hash;
        }

        public override String ToString() {
            return String.Format("{0}:{1}, Node-{2}", Host, Port, NodeId);
        }

        public static Boolean operator ==(Broker value1, Broker value2) {
            return Object.Equals(value1, value2);
        }

        public static Boolean operator !=(Broker value1, Broker value2) {
            return !Object.Equals(value1, value2);
        }
    }

    //TopicMetadata => TopicErrorCode TopicName [PartitionMetadata]
    //  TopicErrorCode => int16
    public class TopicMetadata : IKafkaReadable, IKafkaWriteable {
        //Possible Error Codes: 
        // UnknownTopic (3)
        // LeaderNotAvailable (5)
        // InvalidTopic (17)
        // TopicAuthorizationFailed (29)
        public ErrorCode TopicErrorCode { get; set; }
        public String TopicName { get; set; }
        public PartitionMetadata[] PartitionMetadatas { get; set; }

        public void FetchFrom(KafkaReader reader) {
            TopicErrorCode = (ErrorCode)reader.ReadInt16();
            TopicName = reader.ReadString();
            PartitionMetadatas = reader.ReadArray<PartitionMetadata>();
        }

        public void SaveTo(KafkaWriter writer) {
            writer.Write((Int16)TopicErrorCode);
            writer.Write(TopicName);
            writer.Write(PartitionMetadatas);
        }
    }

    //PartitionMetadata => PartitionErrorCode PartitionId Leader Replicas Isr
    //  PartitionErrorCode => int16
    //  PartitionId => int32
    //  Leader => int32
    //  Replicas => [int32]
    //  Isr => [int32]  
    public class PartitionMetadata : IKafkaReadable, IKafkaWriteable {
        public ErrorCode PartitionErrorCode { get; set; }
        public Int32 PartitionId { get; set; }
        public Int32 Leader { get; set; }
        public Int32[] Replicas { get; set; }
        public Int32[] Isr { get; set; }

        public void FetchFrom(KafkaReader reader) {
            PartitionErrorCode = (ErrorCode)reader.ReadInt16();
            PartitionId = reader.ReadInt32();
            Leader = reader.ReadInt32();
            Replicas = reader.ReadInt32Array();
            Isr = reader.ReadInt32Array();
        }

        public void SaveTo(KafkaWriter writer) {
            writer.Write((Int16)PartitionErrorCode);
            writer.Write(PartitionId);
            writer.Write(Leader);
            writer.Write(Replicas);
            writer.Write(Isr);
        }
    }
}

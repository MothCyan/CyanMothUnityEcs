using System;

namespace CyanMothUnityEcs
{
    /// <summary>
    /// 对外暴露的实体句柄。
    /// Id 用来查内部数组，Version 用来防止旧句柄误操作已经复用的实体槽位。
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        public static readonly Entity Null = new Entity(0, 0);

        public readonly int Id;
        public readonly int Version;

        public bool IsNull => Id == 0;

        public Entity(int id, int version)
        {
            Id = id;
            Version = version;
        }

        public bool Equals(Entity other)
        {
            return Id == other.Id && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ Version;
            }
        }

        public override string ToString()
        {
            return IsNull ? "Entity.Null" : $"Entity({Id}:{Version})";
        }

        public static bool operator ==(Entity left, Entity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Entity left, Entity right)
        {
            return !left.Equals(right);
        }
    }
}

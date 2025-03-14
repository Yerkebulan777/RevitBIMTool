using System.Collections.Generic;


namespace ServiceLibrary.Models
{
    public class TaskRequestComparer : IEqualityComparer<TaskRequest>
    {
        public bool Equals(TaskRequest x, TaskRequest y)
        {
            if (x is null || y is null)
            {
                return false;
            }

            if (x.HashCode == 0)
            {
                x.HashCode = x.GetHashCode();
            }

            if (y.HashCode == 0)
            {
                y.HashCode = y.GetHashCode();
            }

            return x.HashCode == y.HashCode;
        }

        public int GetHashCode(TaskRequest obj)
        {
            return obj.GetHashCode();
        }
    }
}


namespace RevitBIMTool.Models
{
    public class UnionSize
    {
        private readonly Dictionary<SizeKey, SizeKey> parent;

        public UnionSize(List<SizeKey> keys)
        {
            parent = keys.ToDictionary(k => k, k => k);
        }

        /// <summary>
        /// Находит корневой элемент для указанного ключа
        /// </summary>
        public SizeKey FindRoot(SizeKey key)
        {
            if (!parent[key].Equals(key))
            {
                parent[key] = FindRoot(parent[key]); // Сжатие пути
            }

            return parent[key];
        }

        /// <summary>
        /// Объединяет две группы
        /// </summary>
        public void Union(SizeKey key1, SizeKey key2, Dictionary<SizeKey, int> groupSizes)
        {
            SizeKey root1 = FindRoot(key1);
            SizeKey root2 = FindRoot(key2);

            if (!root1.Equals(root2))
            {
                // Всегда делаем корнем большую группу
                if (groupSizes[root1] < groupSizes[root2])
                {
                    parent[root1] = root2;
                }
                else
                {
                    parent[root2] = root1;
                }
            }
        }
    }
}
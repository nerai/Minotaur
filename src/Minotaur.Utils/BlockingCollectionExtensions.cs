using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minotaur.Utils;

public static class BlockingCollectionExtensions
{
	public static Partitioner<T> GetConsumingPartitioner<T> (this BlockingCollection<T> collection)
	{
		return new BlockingCollectionPartitioner<T> (collection);
	}

	public class BlockingCollectionPartitioner<T> : Partitioner<T>
	{
		private readonly BlockingCollection<T> _Collection;

		internal BlockingCollectionPartitioner (BlockingCollection<T> collection)
		{
			_Collection = collection ?? throw new ArgumentNullException ("collection");
		}

		public override bool SupportsDynamicPartitions => true;

		public override IList<IEnumerator<T>> GetPartitions (int partitionCount)
		{
			if (partitionCount < 1) {
				throw new ArgumentOutOfRangeException ("partitionCount");
			}

			var dynamicPartitioner = GetDynamicPartitions ();
			return Enumerable
				.Range (0, partitionCount)
				.Select (_ => dynamicPartitioner.GetEnumerator ())
				.ToArray ();
		}

		public override IEnumerable<T> GetDynamicPartitions ()
		{
			return _Collection.GetConsumingEnumerable ();
		}
	}
}

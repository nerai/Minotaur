using System.Collections.Immutable;
using System.Runtime.Serialization;
using Minotaur.Boards.Strings;

namespace Minotaur.Boards.TreeCaches;

internal class ImmutableArraySurrogate : ISerializationSurrogateProvider
{
	[DataContract]
	private class ImmutableArraySubst<T>
	{
		[DataMember]
		public T [] Immut;
	}

	public object GetDeserializedObject (object obj, Type targetType)
	{
		if (obj is ImmutableArraySubst<Worm> a) {
			return a.Immut.ToImmutableArray ();
		}
		return obj;
	}

	public object GetObjectToSerialize (object obj, Type targetType)
	{
		if (obj is ImmutableArray<Worm> a) {
			return new ImmutableArraySubst<Worm> () {
				Immut = a.ToArray ()
			};
		}
		return obj;
	}

	public Type GetSurrogateType (Type type)
	{
		if (type == typeof (ImmutableArray<Worm>)) {
			return typeof (ImmutableArraySubst<Worm>);
		}
		return null;
	}
}

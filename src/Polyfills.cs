// Attributs que le compilateur C# génère pour certaines constructions (tuples
// nommés, lambdas async...). Une des DLL IL2CPP du jeu contient une version
// incomplète de NullableAttribute, ce qui provoque l'erreur
// « Membre requis par le compilateur 'NullableAttribute..ctor' manquant ».
// Déclarer nos propres versions ici règle le problème : le compilateur
// utilise en priorité celles du projet.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;
        public NullableAttribute(byte flag) { NullableFlags = new[] { flag }; }
        public NullableAttribute(byte[] flags) { NullableFlags = flags; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Interface |
                    AttributeTargets.Method | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;
        public NullableContextAttribute(byte flag) { Flag = flag; }
    }
}

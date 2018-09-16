using Phantasma.Numerics;

namespace Phantasma.Cryptography
{
    internal class MerkleTreeNode
    {
        public Hash Hash;
        public MerkleTreeNode Parent;
        public MerkleTreeNode LeftChild;
        public MerkleTreeNode RightChild;

        public bool IsLeaf => LeftChild == null && RightChild == null;

        public bool IsRoot => Parent == null;
    }
}

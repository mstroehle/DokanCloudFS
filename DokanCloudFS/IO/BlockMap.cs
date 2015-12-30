﻿/*
The MIT License(MIT)

Copyright(c) 2015 IgorSoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IgorSoft.DokanCloudFS.IO
{
    [System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class BlockMap
    {
        [System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
        internal sealed class Block : IComparable<Block>, IComparable, IEquatable<Block>
        {
            public int Offset { get; private set; }

            public int Count { get; private set; }

            public Block(int offset, int count)
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} must be nonnegative.");
                if (count <= 0)
                    throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(count)} must be positive.");

                Offset = offset;
                Count = count;
            }

            public bool Contains(Block other) => Offset <= other.Offset && Offset + Count >= other.Offset + other.Count;

            public bool Preceeds(Block other) => Offset <= other.Offset;

            public bool ImmediatelyPreceeds(Block other) => Offset + Count == other.Offset;

            public bool Intersects(Block other) => Preceeds(other) && Offset + Count > other.Offset || Succeeds(other) && Offset < other.Offset + other.Count;

            public bool ImmediatelySucceeds(Block other) => other.ImmediatelyPreceeds(this);

            public bool Succeeds(Block other) => other.Preceeds(this);

            public bool TryMerge(Block other)
            {
                if (ImmediatelyPreceeds(other)) {
                    Count += other.Count;
                    return true;
                } else if (ImmediatelySucceeds(other)) {
                    Offset = other.Offset;
                    Count += other.Count;
                    return true;
                } else {
                    return false;
                }
            }

            public int CompareTo(Block other) => Offset.CompareTo(other.Offset);

            public int CompareTo(object obj) => -(obj as Block)?.CompareTo(this) ?? -1;

            public bool Equals(Block other) => Offset == other.Offset && Count == other.Count;

            public override bool Equals(object obj) => (obj as Block)?.Equals(this) ?? false;

            public override int GetHashCode() => Offset.GetHashCode() ^ Count.GetHashCode();

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Debugger Display")]
            [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            private string DebuggerDisplay => $"{nameof(Block)}({Offset}, {Count})";
        }

        private List<Block> blocks = new List<Block>();

        public ReadOnlyCollection<Block> Blocks => new ReadOnlyCollection<Block>(blocks);

        public int Capacity { get; }

        public BlockMap(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), $"{nameof(capacity)} must be positive.");

            Capacity = capacity;
        }

        private int GetAvailableBytes(int index, int offset, int count)
        {
            return Math.Min(count, Math.Max(0, blocks[index].Count - (offset - blocks[index].Offset)));
        }

        public int GetAvailableBytes(int offset, int count)
        {
            if (offset < 0 || offset > Capacity)
                throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)}({offset}) is negative or exceeds {nameof(Capacity)}({Capacity}).");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(count)}({count}) is negative.");
            count = Math.Min(count, Capacity - offset);

            if (count == 0)
                return 0;

            int index = blocks.BinarySearch(new Block(offset, count));

            return index >= 0
                ? GetAvailableBytes(index, offset, count)
                : ~index > 0
                    ? GetAvailableBytes(~index - 1, offset, count)
                    : 0;
        }

        public void AssignBytes(int offset, int count)
        {
            if (offset >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)}({offset}) exceeds {nameof(Capacity)}({Capacity}).");
            if (offset + count > Capacity)
                throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(count)}({count}) exceedes remaining {nameof(Capacity)}).");

            var block = new Block(offset, count);

            int index = blocks.BinarySearch(block), successorIndex = ~index;

            if (index >= 0 || successorIndex > 0 && blocks[successorIndex - 1].Intersects(block) || successorIndex < blocks.Count && blocks[successorIndex].Intersects(block))
                throw new InvalidOperationException($"{nameof(Block)}({offset}, {count}) intersects previous coverage.");

            var predecessor = successorIndex > 0 ? blocks[successorIndex - 1] : null;
            var successor = successorIndex < blocks.Count ? blocks[successorIndex] : null;

            if (predecessor?.TryMerge(block) ?? false) {
                if (successor?.TryMerge(predecessor) ?? false)
                    blocks.Remove(predecessor);
            } else if (successor?.TryMerge(block) ?? false) {
                // Nothing to do here
            } else {
                blocks.Insert(successorIndex, block);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Debugger Display")]
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private string DebuggerDisplay => $"{nameof(BlockMap)}[{Capacity}]: {string.Join(",", blocks.Select(b => $"({b.Offset}|{b.Count})"))}";
    }
}
﻿using System;
using System.Collections;
using System.IO;
using System.Text;
using FortniteReplayReaderDecompressor.Core.Models;

namespace FortniteReplayReaderDecompressor.Core
{
    /// <summary>
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Public/Serialization/BitArchive.h
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Private/Serialization/BitArchive.cpp
    /// </summary>
    public class BitReader : FBitArchive
    {
        private readonly BitArray Bits;

        /// <summary>
        /// Position in current BitArray. Set with <see cref="Seek(int, SeekOrigin)"/>
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// For pushing and popping FBitReaderMark positions.
        /// </summary>
        public int MarkPosition { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of the BitReader class based on the specified bytes.
        /// </summary>
        /// <param name="input">The input bytes.</param>
        /// <exception cref="System.ArgumentException">The stream does not support reading, is null, or is already closed.</exception>
        public BitReader(byte[] input)
        {
            Bits = new BitArray(input);
        }

        /// <summary>
        /// Initializes a new instance of the BitReader class based on the specified bool[].
        /// </summary>
        /// <param name="input">The input bool[].</param>

        public BitReader(bool[] input)
        {
            Bits = new BitArray(input);
        }

        /// <summary>
        /// Returns whether <see cref="Position"/> in current <see cref="Bits"/> is greater than the lenght of the current <see cref="Bits"/>.
        /// </summary>
        /// <returns>true, if <see cref="Position"/> is greater than lenght, false otherwise</returns>
        public override bool AtEnd()
        {
            return Position >= Bits.Length;
        }

        /// <summary>
        /// Returns the bit at <see cref="Position"/> and does not advance the <see cref="Position"/> by one bit.
        /// </summary>
        /// <returns>The value of the bit at position index.</returns>
        /// <seealso cref="ReadBit"/>
        public override bool PeekBit()
        {
            return Bits[Position];
        }

        /// <summary>
        /// Returns the bit at <see cref="Position"/> and advances the <see cref="Position"/> by one bit.
        /// </summary>
        /// <returns>The value of the bit at position index.</returns>
        /// <seealso cref="PeekBit"/>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public override bool ReadBit()
        {
            return Bits[Position++];
        }

        public override T[] ReadArray<T>(Func<T> func1)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Retuns bool[] and advances the <see cref="Position"/> by <paramref name="bits"/> bits.
        /// </summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>bool[]</returns>
        public override bool[] ReadBits(int bitCount)
        {
            bool[] result = new bool[bitCount];
            for (var i = 0; i < bitCount; i++)
            {
                result[i] = ReadBit();
            }
            return result;
        }

        /// <summary>
        /// Retuns bool[] and advances the <see cref="Position"/> by <paramref name="bits"/> bits.
        /// </summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>bool[]</returns>
        public override bool[] ReadBits(uint bitCount)
        {
            return ReadBits((int)bitCount);
        }

        /// <summary>
        /// Returns the bit at <see cref="Position"/> and advances the <see cref="Position"/> by one bit.
        /// </summary>
        /// <returns>The value of the bit at position index.</returns>
        /// <seealso cref="PeekBit"/>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public override bool ReadBoolean()
        {
            return ReadBit();
        }

        /// <summary>
        /// Returns the byte at <see cref="Position"/> and advances the <see cref="Position"/> by 8 bits.
        /// </summary>
        /// <returns>The value of the byte at <see cref="Position"/> index.</returns>
        public override byte ReadByte()
        {
            var result = new byte();
            for (int i = 0; i < 8; i++)
            {
                if (ReadBit())
                {
                    result |= (byte)(1 << i);
                }
            }

            return result;
        }

        public override T ReadByteAsEnum<T>()
        {
            throw new NotImplementedException();
        }

        public override byte[] ReadBytes(int byteCount)
        {
            var result = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                result[i] = ReadByte();
            }
            return result;
        }

        public override byte[] ReadBytes(uint byteCount)
        {
            return ReadBytes((int)byteCount);
        }

        public override string ReadBytesToString(int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Private/Containers/String.cpp#L1390
        /// </summary>
        /// <returns>string</returns>
        public override string ReadFString()
        {
            var length = ReadInt32();

            if (length == 0)
            {
                return "";
            }

            var isUnicode = length < 0;
            byte[] data;
            string value;

            if (isUnicode)
            {
                length = -2 * length;
                data = ReadBytes(length);
                value = Encoding.Unicode.GetString(data);
            }
            else
            {
                data = ReadBytes(length);
                value = Encoding.Default.GetString(data);
            }

            return value.Trim(new[] { ' ', '\0' });
        }

        public override string ReadGUID()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Retuns uint.
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Public/Serialization/BitReader.h#L69
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns>uint</returns>
        /// <exception cref="OverflowException"></exception>
        public override uint ReadInt(int maxValue)
        {
            uint value = 0;
            for (uint mask = 1; (value + mask) < maxValue; mask *= 2)
            {
                if (ReadBit())
                {
                    value |= mask;
                }
            }

            return value;
        }

        public override short ReadInt16()
        {
            return BitConverter.ToInt16(ReadBytes(2));
        }

        public override int ReadInt32()
        {
            return BitConverter.ToInt32(ReadBytes(4));
        }

        public override bool ReadInt32AsBoolean()
        {
            return ReadInt32() == 1;
        }

        public override long ReadInt64()
        {
            return BitConverter.ToInt64(ReadBytes(8));
        }

        /// <summary>
        /// Retuns uint
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Private/Serialization/BitReader.cpp#L254
        /// </summary>
        /// <returns>uint</returns>
        public override uint ReadIntPacked()
        {
            uint value = 0;
            bool remaining;

            for (var it = 0; it < 5; it++)
            {
                remaining = ReadBit(); // Check 1 bit to see if theres more after this
                for (int i = 0; i < 7; i++)
                {
                    if (ReadBit())
                    {
                        value |= (byte)(1 << i); // Add to total value
                    }
                }
                if (!remaining)
                {
                    break;
                }
            }
            return value;
        }

        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Classes/Engine/NetSerialization.h#L1210
        /// </summary>
        /// <returns>Vector</returns>
        public override FVector ReadPackedVector(int scaleFactor, int maxBits)
        {
            var bits = ReadInt(maxBits);
            var bias = 1 << ((int)bits + 1);
            var max = 1 << ((int)bits + 2);

            var dx = ReadInt(max);
            var dy = ReadInt(max);
            var dz = ReadInt(max);

            var x = dx - bias / scaleFactor;
            var y = dy - bias / scaleFactor;
            var z = dz - bias / scaleFactor;

            return new FVector(x, y, z);
        }

        public override sbyte ReadSByte()
        {
            throw new NotImplementedException();
        }

        public override float ReadSingle()
        {
            return BitConverter.ToSingle(ReadBytes(4));
        }

        public override (T, U)[] ReadTupleArray<T, U>(Func<T> func1, Func<U> func2)
        {
            throw new NotImplementedException();
        }

        public override ushort ReadUInt16()
        {
            return BitConverter.ToUInt16(ReadBytes(2));
        }

        public override uint ReadUInt32()
        {
            return BitConverter.ToUInt32(ReadBytes(4));
        }

        public override bool ReadUInt32AsBoolean()
        {
            throw new NotImplementedException();
        }

        public override T ReadUInt32AsEnum<T>()
        {
            throw new NotImplementedException();
        }

        public override ulong ReadUInt64()
        {
            return BitConverter.ToUInt64(ReadBytes(8));
        }

        /// <summary>
        /// Sets <see cref="Position"/> within current BitArray.
        /// </summary>
        /// <param name="offset">The offset relative to the <paramref name="seekOrigin"/>.</param>
        /// <param name="seekOrigin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Begin)
        {
            if (offset < 0 || offset > Bits.Length || (seekOrigin == SeekOrigin.Current && offset + Position > Bits.Length))
            {
                throw new ArgumentOutOfRangeException("Specified offset doesnt fit within the BitArray buffer");
            }

            _ = (seekOrigin switch
            {
                SeekOrigin.Begin => Position = offset,
                SeekOrigin.End => Position = Bits.Length - offset,
                SeekOrigin.Current => Position += offset,
                _ => Position = offset,
            });
        }

        public override void SkipBytes(uint byteCount)
        {
            throw new NotImplementedException();
        }

        public override void SkipBytes(int byteCount)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Save Position to <see cref="MarkPosition"/> so we can reset back to this point.
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Public/Serialization/BitReader.h#L228
        /// </summary>
        public override void Mark()
        {
            MarkPosition = Position;
        }


        /// <summary>
        /// Set Position back to <see cref="MarkPosition"/>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Public/Serialization/BitReader.h#L228
        /// </summary>
        public override void Pop()
        {
            // TODO: pop makes it sound like a list...
            Position = MarkPosition;
        }

        public override int GetBitsLeft()
        {
            return Bits.Length - Position;
        }

        public override void AppendDataFromChecked(bool[] data)
        {
            throw new NotImplementedException();
        }
    }
}
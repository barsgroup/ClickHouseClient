﻿#region License Apache 2.0
/* Copyright 2019-2021 Octonica
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Octonica.ClickHouseClient.Exceptions;
using Octonica.ClickHouseClient.Protocol;
using Octonica.ClickHouseClient.Utils;

namespace Octonica.ClickHouseClient.Types
{
    internal sealed class UInt64TypeInfo : SimpleTypeInfo
    {
        public UInt64TypeInfo()
            : base("UInt64")
        {
        }

        public override IClickHouseColumnReader CreateColumnReader(int rowCount)
        {
            return new UInt64Reader(rowCount);
        }

        public override IClickHouseColumnReaderBase CreateSkippingColumnReader(int rowCount)
        {
            return new SimpleSkippingColumnReader(sizeof(ulong), rowCount);
        }

        public override IClickHouseColumnWriter CreateColumnWriter<T>(string columnName, IReadOnlyList<T> rows, ClickHouseColumnSettings? columnSettings)
        {
            var type = typeof(T);
            IReadOnlyList<ulong> ulongRows;

            if (type == typeof(ulong))
                ulongRows = (IReadOnlyList<ulong>)rows;
            else if (type == typeof(uint))
                ulongRows = MappedReadOnlyList<uint, ulong>.Map((IReadOnlyList<uint>)rows, v => v);
            else if (type == typeof(ushort))
                ulongRows = MappedReadOnlyList<ushort, ulong>.Map((IReadOnlyList<ushort>)rows, v => v);
            else if (type == typeof(byte))
                ulongRows = MappedReadOnlyList<byte, ulong>.Map((IReadOnlyList<byte>)rows, v => v);
            else
                throw new ClickHouseException(ClickHouseErrorCodes.TypeNotSupported, $"The type \"{typeof(T)}\" can't be converted to the ClickHouse type \"{ComplexTypeName}\".");

            return new UInt64Writer(columnName, ComplexTypeName, ulongRows);
        }

        public override void FormatValue(StringBuilder queryStringBuilder, object? value)
        {
            if (value == null || value is DBNull)
                throw new ClickHouseException(ClickHouseErrorCodes.TypeNotSupported, $"The ClickHouse type \"{ComplexTypeName}\" does not allow null values");

            ulong outputValue;
            
            if (value is ulong ulongValue)
                outputValue = ulongValue;
            else if (value is uint uintValue)
                outputValue = uintValue;
            else if (value is ushort ushortValue)
                outputValue = ushortValue;
            else if (value is byte byteValue)
                outputValue = byteValue;
            else
                throw new ClickHouseException(ClickHouseErrorCodes.TypeNotSupported, $"The type \"{value.GetType()}\" can't be converted to the ClickHouse type \"{ComplexTypeName}\".");
            
            queryStringBuilder.Append(outputValue.ToString(CultureInfo.InvariantCulture));
        }

        public override Type GetFieldType()
        {
            return typeof(ulong);
        }

        public override ClickHouseDbType GetDbType()
        {
            return ClickHouseDbType.UInt64;
        }

        private sealed class UInt64Reader : StructureReaderBase<ulong>
        {
            protected override bool BitwiseCopyAllowed => true;

            public UInt64Reader(int rowCount)
                : base(sizeof(ulong), rowCount)
            {
            }

            protected override ulong ReadElement(ReadOnlySpan<byte> source)
            {
                return BitConverter.ToUInt64(source);
            }
        }

        internal sealed class UInt64Writer : StructureWriterBase<ulong>
        {
            protected override bool BitwiseCopyAllowed => true;

            public UInt64Writer(string columnName, string columnType, IReadOnlyList<ulong> rows)
                : base(columnName, columnType, sizeof(ulong), rows)
            {
            }

            protected override void WriteElement(Span<byte> writeTo, in ulong value)
            {
                var success = BitConverter.TryWriteBytes(writeTo, value);
                Debug.Assert(success);
            }
        }
    }
}

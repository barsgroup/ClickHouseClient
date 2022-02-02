﻿#region License Apache 2.0
/* Copyright 2019-2022 Octonica
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
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Octonica.ClickHouseClient.Tests
{
    public class ClickHouseDataReaderTests : ClickHouseTestsBase
    {
        [Fact]
        public async Task SimpleReader()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select 42 as answer";

            await using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None);
            var columnIndex = reader.GetOrdinal("answer");
            Assert.True(await reader.ReadAsync(CancellationToken.None));
            var value = reader.GetByte(columnIndex);
            Assert.False(await reader.ReadAsync());

            Assert.Equal<byte>(42, value);
        }

        [Fact]
        public async Task CloseReaderWithoutReading()
        {
            await using var cn = await OpenConnectionAsync();

            await using (var cmd = cn.CreateCommand("SELECT * FROM system.numbers"))
            {
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    ulong value = 0;
                    while (value < 100 && await reader.ReadAsync())
                        value = reader.GetFieldValue<ulong>(0);

                    Assert.Equal<ulong>(100, value);
                }

                await using var reader2 = await cmd.ExecuteReaderAsync(CancellationToken.None);
                Assert.True(await reader2.ReadAsync());

                var value2 = reader2.GetFieldValue<ulong>(0);
                Assert.Equal<ulong>(0, value2);
            }

            await using var cmd2 = cn.CreateCommand("SELECT * FROM system.one");

            await (await cmd2.ExecuteReaderAsync()).DisposeAsync();

            cmd2.CommandText = "SELECT * FROM system.numbers LIMIT 10000 OFFSET 42";

            await using var reader3 = await cmd2.ExecuteReaderAsync();
            Assert.True(await reader3.ReadAsync());
            var value3 = reader3.GetFieldValue<ulong>(0);

            Assert.Equal<ulong>(42, value3);
        }

        [Fact]
        public async Task TotalsWithNextResult()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select x, sum(y) as v from (SELECT number%2 + 1 as x, number as y FROM numbers(10)) group by x with totals;";

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(ClickHouseDataReaderState.Data, reader.State);

            ulong rowsTotal = 0;
            while (reader.Read())
            {
                Assert.Equal(ClickHouseDataReaderState.Data, reader.State);
                rowsTotal += reader.GetFieldValue<ulong>(1);
            }

            Assert.Equal(ClickHouseDataReaderState.NextResultPending, reader.State);
            var hasTotals = reader.NextResult();
            Assert.True(hasTotals);
            Assert.Equal(ClickHouseDataReaderState.Totals, reader.State);

            Assert.True(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.Totals, reader.State);
            var total = reader.GetFieldValue<ulong>(1);
            Assert.Equal(rowsTotal, total);

            Assert.False(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.Closed, reader.State);
        }

        [Fact]
        public async Task ExtremesWithNextResult()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.Extremes = true;
            cmd.CommandText = "select x, sum(y) as v from (SELECT number%2 + 1 as x, number as y FROM numbers(10)) group by x;";

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(ClickHouseDataReaderState.Data, reader.State);

            ulong minX = ulong.MaxValue, maxX = ulong.MinValue, minSum = ulong.MaxValue, maxSum = ulong.MinValue;
            while (reader.Read())
            {
                Assert.Equal(ClickHouseDataReaderState.Data, reader.State);
                var x = reader.GetFieldValue<ulong>(0);
                var sum = reader.GetFieldValue<ulong>(1);

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);

                minSum = Math.Min(minSum, sum);
                maxSum = Math.Max(maxSum, sum);
            }

            Assert.Equal(ClickHouseDataReaderState.NextResultPending, reader.State);

            Assert.True(reader.NextResult());

            Assert.True(reader.Read());
            var extremeX = reader.GetFieldValue<ulong>(0);
            var extremeSum = reader.GetFieldValue<ulong>(1);
            Assert.Equal(minX, extremeX);
            Assert.Equal(minSum, extremeSum);

            Assert.True(reader.Read());
            extremeX = reader.GetFieldValue<ulong>(0);
            extremeSum = reader.GetFieldValue<ulong>(1);
            Assert.Equal(maxX, extremeX);
            Assert.Equal(maxSum, extremeSum);

            Assert.False(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.Closed, reader.State);
        }

        [Fact]
        public async Task TotalsAndExtremes()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.Extremes = true;
            cmd.CommandText = "select x, sum(y) as v from (SELECT number%2 + 1 as x, number as y FROM numbers(10)) group by x with totals;";

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(ClickHouseDataReaderState.Data, reader.State);

            ulong rowsTotal = 0;
            ulong minX = ulong.MaxValue, maxX = ulong.MinValue, minSum = ulong.MaxValue, maxSum = ulong.MinValue;
            while (reader.Read())
            {
                Assert.Equal(ClickHouseDataReaderState.Data, reader.State);
                var x = reader.GetFieldValue<ulong>(0);
                var sum = reader.GetFieldValue<ulong>(1);

                rowsTotal += sum;

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);

                minSum = Math.Min(minSum, sum);
                maxSum = Math.Max(maxSum, sum);
            }

            Assert.Equal(ClickHouseDataReaderState.NextResultPending, reader.State);
            var hasTotals = reader.NextResult();
            Assert.True(hasTotals);
            Assert.Equal(ClickHouseDataReaderState.Totals, reader.State);

            Assert.True(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.Totals, reader.State);
            var total = reader.GetFieldValue<ulong>(1);
            Assert.Equal(rowsTotal, total);

            Assert.False(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.NextResultPending, reader.State);

            Assert.True(reader.NextResult());

            Assert.True(reader.Read());
            var extremeX = reader.GetFieldValue<ulong>(0);
            var extremeSum = reader.GetFieldValue<ulong>(1);
            Assert.Equal(minX, extremeX);
            Assert.Equal(minSum, extremeSum);

            Assert.True(reader.Read());
            extremeX = reader.GetFieldValue<ulong>(0);
            extremeSum = reader.GetFieldValue<ulong>(1);
            Assert.Equal(maxX, extremeX);
            Assert.Equal(maxSum, extremeSum);

            Assert.False(reader.Read());

            Assert.Equal(ClickHouseDataReaderState.Closed, reader.State);
        }

        [Theory]
        [InlineData(ClickHouseDataReaderState.Data, 3)]
        [InlineData(ClickHouseDataReaderState.Extremes, 2)]
        [InlineData(ClickHouseDataReaderState.Totals, 1)]
        public async Task SkipNextResult(ClickHouseDataReaderState readBlock, int expectedCount)
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.Extremes = true;
            cmd.CommandText = "select x, sum(y) as v from (SELECT number%3 + 1 as x, number as y FROM numbers(10)) group by x with totals;";

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(ClickHouseDataReaderState.Data, reader.State);

            do
            {
                switch (reader.State)
                {
                    case ClickHouseDataReaderState.Data:
                    case ClickHouseDataReaderState.Totals:
                    case ClickHouseDataReaderState.Extremes:
                        if (reader.State == readBlock)
                            break;

                        continue;

                    default:
                        Assert.True(false, $"Unexpected state: {reader.State}.");
                        break;
                }

                int count = 0;
                while (await reader.ReadAsync())
                    ++count;

                Assert.False(await reader.ReadAsync());
                Assert.Equal(expectedCount, count);
                Assert.True(reader.State == ClickHouseDataReaderState.NextResultPending || reader.State == ClickHouseDataReaderState.Closed);

            } while (await reader.NextResultAsync());

            Assert.Equal(ClickHouseDataReaderState.Closed, reader.State);
        }

        [Fact]
        public async Task CommandBehaviorSchemaOnly()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand("SELECT * FROM system.numbers");
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly);

            Assert.False(await reader.ReadAsync());
            Assert.False(await reader.ReadAsync());
            Assert.False(await reader.NextResultAsync());
            Assert.False(await reader.ReadAsync());

            Assert.Equal(1, reader.FieldCount);
        }

        [Fact]
        public async Task CommandBehaviorSingleRow()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand("SELECT * FROM system.numbers");
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

            Assert.Equal(1, reader.FieldCount);
            Assert.True(await reader.ReadAsync());

            Assert.False(await reader.ReadAsync());
            Assert.False(await reader.ReadAsync());
            Assert.False(await reader.NextResultAsync());
            Assert.False(await reader.ReadAsync());
        }

        [Fact]
        public async Task CommandBehaviorSingleResult()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand("select x, sum(y) as v from (SELECT number%7 + 1 as x, number as y FROM numbers(100)) group by x with totals;");
            cmd.Extremes = true;

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult);

            int count = 0;
            while (await reader.ReadAsync())
                ++count;

            Assert.Equal(7, count);
            Assert.False(reader.Read());
            Assert.NotEqual(ClickHouseDataReaderState.NextResultPending, reader.State);

            Assert.False(reader.NextResult());
            Assert.False(reader.Read());
        }

        [Fact]
        public async Task CommandBehaviorSequentialAccess()
        {
            // SequentialAccess is ignored

            await using var cn = await OpenConnectionAsync();
            await using var cmd = cn.CreateCommand("SELECT 41,42,43");
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            Assert.True(await reader.ReadAsync());

            Assert.Equal(41, reader.GetInt32(0));
            Assert.Equal(42, reader.GetInt32(1));
            Assert.Equal(43, reader.GetInt32(2));

            Assert.False(await reader.ReadAsync());
        }

        [Fact]
        public async Task CommandBehaviorKeyInfo()
        {
            // KeyInfo is not supported

            await using var cn = await OpenConnectionAsync();
            await using var cmd = cn.CreateCommand("SELECT 42");
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo));
            Assert.Equal("behavior", ex.ParamName);
        }

        [Fact]
        public async Task ProfileEvents()
        {
            await using var cn = await OpenConnectionAsync();

            const int expectedCount = 200_000;
            await using var cmd = cn.CreateCommand(string.Format(CultureInfo.InvariantCulture, "SELECT number as n, addSeconds('2000-01-01 00:00:00'::DateTime, n) as d FROM numbers({0})", expectedCount));
            cmd.IgnoreProfileEvents = false;

            int count = 0, eventTableCount = 0;
            long selectedRows = 0;
            await using var reader = await cmd.ExecuteReaderAsync();
            reader.ConfigureColumn("d", new ClickHouseColumnSettings(columnType: typeof(DateTime)));

            var startDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, -cn.GetServerTimeZone().GetUtcOffset(new DateTime(2000, 1, 1)));

            while(true)
            {
                Assert.Equal(ClickHouseDataReaderState.Data, reader.State);

                while (await reader.ReadAsync())
                {
                    var number = reader.GetUInt64(0);
                    var dateObj = reader.GetValue(1);

                    // Check that column settings applied
                    var date = Assert.IsType<DateTime>(dateObj);

                    Assert.Equal(startDate.AddSeconds(number).DateTime, date);
                    ++count;
                }

                if (!await reader.NextResultAsync())
                    break;

                Assert.Equal(ClickHouseDataReaderState.ProfileEvents, reader.State);

                var typeColumnIdx = reader.GetOrdinal("type");
                var nameColumnIdx = reader.GetOrdinal("name");
                var valueColumnIdx = reader.GetOrdinal("value");

                ++eventTableCount;
                while(await reader.ReadAsync())
                {
                    var name = reader.GetString(nameColumnIdx);
                    if (name != "SelectedRows")
                        continue;

                    var type = reader.GetString(typeColumnIdx);
                    var value = reader.GetInt64(valueColumnIdx);
                    switch (type)
                    {
                        case "increment":
                            selectedRows = value;
                            break;
                        default:
                            Assert.True(false, $"Unexpected event type: {type}");
                            continue;
                    }
                }

                if (!await reader.NextResultAsync())
                    break;
            };

            Assert.True(eventTableCount > 1);
            Assert.Equal(expectedCount, count);
            Assert.True(selectedRows >= expectedCount);
        }

        [Fact]
        public async Task DateTimeKindForDate()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select cast('2022-01-01' as Date)";

            await using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None);
            Assert.True(await reader.ReadAsync(CancellationToken.None));
            var value = reader.GetDateTime(0);
            Assert.False(await reader.ReadAsync());

            Assert.Equal(2022, value.Year);
            Assert.Equal(1, value.Month);
            Assert.Equal(1, value.Day);
            Assert.Equal(0, value.Hour);
            Assert.Equal(0, value.Minute);
            Assert.Equal(0, value.Second);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        [Fact]
        public async Task DateTimeKindForDate32()
        {
            await using var cn = await OpenConnectionAsync();

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select cast('2022-01-01' as Date32)";

            await using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None);
            Assert.True(await reader.ReadAsync(CancellationToken.None));
            var value = reader.GetDateTime(0);
            Assert.False(await reader.ReadAsync());

            Assert.Equal(2022, value.Year);
            Assert.Equal(1, value.Month);
            Assert.Equal(1, value.Day);
            Assert.Equal(0, value.Hour);
            Assert.Equal(0, value.Minute);
            Assert.Equal(0, value.Second);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }
    }
}

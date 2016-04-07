﻿//  Copyright (c) 2002-2016 "Neo Technology,"
//  Network Engine for Objects in Lund AB [http://neotechnology.com]
// 
//  This file is part of Neo4j.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Neo4j.Driver.Internal.Result;
using Record = Neo4j.Driver.Internal.Result.Record;

namespace Neo4j.Driver.Tests
{
    public class StatementResultTests
    {
        private class ListBasedRecordSet : IRecordSet
    {
        private readonly IList<IRecord> _records;
        private int _recordIndex = 0;

        public ListBasedRecordSet(IList<IRecord> records)
        {
            _records = records;
        }

        public bool AtEnd
        {
            get
            {
                return _recordIndex >= _records.Count;
            }
        }

        public IRecord Peek
        {
            get
            {
                if (_recordIndex >= _records.Count) return null;

                return _records[_recordIndex];
            }
        }

        public int Position
        {
            get
            {
                return _recordIndex - 1;
            }
        }

        public IEnumerable<IRecord> Records
        {
            get
            {
                if (_recordIndex >= _records.Count) yield break;

                while (_recordIndex < _records.Count)
                {
                    yield return _records[_recordIndex++];
                }

                _recordIndex++;
                yield break;
            }
        }
    }

    private class ResultCreator
    {
        public static StatementResult CreateResult(int keySize, int recordSize=1, Func<IResultSummary> getSummaryFunc = null)
        {
            var records = new List<IRecord>(recordSize);

            var keys = new List<string>(keySize);
            for (int i = 0; i < keySize; i++)
            {
                keys.Add($"str{i}");
            }

            for (int j = 0; j < recordSize; j++)
            {
                var values = new List<object>();
                for (int i = 0; i < keySize; i++)
                {
                    values.Add(i);
                }
                records.Add(new Record(keys.ToArray(), values.ToArray()));
            }
            
            return new StatementResult(keys.ToArray(), new ListBasedRecordSet(records), getSummaryFunc);
        }
    }
    
        public class Constructor
        {
            [Fact]
            public void ShouldThrowArgumentNullExceptionIfRecordsIsNull()
            {
                var ex = Xunit.Record.Exception(() => new StatementResult(new string[] {"test"}, null));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<ArgumentNullException>();
            }

            [Fact]
            public void ShouldThrowArgumentNullExceptionIfKeysIsNull()
            {
                var ex = Xunit.Record.Exception(() => new StatementResult(null, new ListBasedRecordSet(new List<IRecord>())));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<ArgumentNullException>();
            }

            [Fact]
            public void ShouldSetKeysProperlyIfKeysNotNull()
            {
                var result = new StatementResult(new string[] {"test"}, new ListBasedRecordSet(new List<IRecord>()));
                result.Keys.Should().HaveCount(1);
                result.Keys.Should().Contain("test");
            }
        }

        public class ConsumeMethod
        {
            // INFO: Rewritten because StatementResult no longers takes IPeekingEnumerator in constructor
            [Fact]
            public void ShouldConsumeAllRecords()
            {
                var result = ResultCreator.CreateResult(0, 3);
                result.Consume();
                result.Count().Should().Be(0);
                result.Peek().Should().BeNull();
                result.Position.Should().Be(3);
            }

            [Fact]
            public void ShouldConsumeSummaryCorrectly()
            {
                int getSummaryCalled = 0;
                var result = ResultCreator.CreateResult(1, 0, () => { getSummaryCalled++; return new FakeSummary(); });


                result.Consume();
                getSummaryCalled.Should().Be(1);

                // the same if we call it multiple times
                result.Consume();
                getSummaryCalled.Should().Be(1);
            }

            [Fact]
            public void ShouldThrowNoExceptionWhenCallingMultipleTimes()
            {
              
                var result = ResultCreator.CreateResult(1);

                result.Consume();
                var ex = Xunit.Record.Exception(() => result.Consume());
                ex.Should().BeNull();
            }

            [Fact]
            public void ShouldConsumeRecordCorrectly()
            {

                var result = ResultCreator.CreateResult(1, 3);

                result.Consume();
                result.Count().Should().Be(0); // the records left after consume
                result.Position.Should().Be(3);

                result.GetEnumerator().Current.Should().BeNull();
                result.GetEnumerator().MoveNext().Should().BeFalse();
            }
        }

        public class StreamingRecords
        {
            private readonly ITestOutputHelper _output;

            private class TestRecordYielder
            {
                private readonly IList<Record> _records = new List<Record>();
                private readonly int _total = 0;

                private readonly ITestOutputHelper _output;
                public static string[] Keys => new[] {"Test", "Keys"};

                public TestRecordYielder(int count, int total, ITestOutputHelper output)
                {
                   Add(count);
                    _total = total;
                    _output = output;
                }

                public void AddNew(int count)
                {
                    Add(count);
                }

                private void Add(int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        _records.Add(new Record(Keys, new object[] { "Test", 123 }));
                    }
                }

                public IEnumerable<Record> Records
                {
                    get
                    {
                        int i = 0;
                        while (i < _total)
                        {
                            while (i == _records.Count)
                            {
                                _output.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")} -> Waiting for more Records");
                                Thread.Sleep(50);
                            }

                            yield return _records[i];
                            i++;
                        }
                    }
                }

                public IEnumerable<Record> RecordsWithAutoLoad
                {
                    get
                    {
                        int i = 0;
                        while (i < _total)
                        {
                            while (i == _records.Count)
                            {
                                _output.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")} -> Waiting for more Records");
                                Thread.Sleep(500);
                                AddNew(1);
                                _output.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")} -> Record arrived");
                            }

                            yield return _records[i];
                            i++;
                        }
                    }
                }
            }

            private class FuncBasedRecordSet : IRecordSet
            {
                private readonly Func<IEnumerable<IRecord>> _getRecords;

                public FuncBasedRecordSet(Func<IEnumerable<IRecord>> getRecords)
                {
                    _getRecords = getRecords;
                }

                public bool AtEnd { get { throw new NotImplementedException(); } }

                public IRecord Peek { get { throw new NotImplementedException(); } }

                public int Position { get { throw new NotImplementedException(); } }

                public IEnumerable<IRecord> Records
                {
                    get
                    {
                        return _getRecords();
                    }
                }
            }

            public StreamingRecords(ITestOutputHelper output)
            {
                _output = output;
            }

            [Fact]
            public void ShouldReturnRecords()
            {
                var recordYielder = new TestRecordYielder(5, 10, _output);
                var cursor = new StatementResult( TestRecordYielder.Keys, new FuncBasedRecordSet(() => recordYielder.RecordsWithAutoLoad));
                var records = cursor.ToList();
                records.Count.Should().Be(10);
            }

            [Fact]
            public void ShouldWaitForAllRecordsToArrive()
            {
                var recordYielder = new TestRecordYielder(5, 10, _output);

                int count = 0;
                var cursor = new StatementResult(TestRecordYielder.Keys, new FuncBasedRecordSet(() => recordYielder.Records));
                var t = Task.Factory.StartNew(() =>
               {
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var item in cursor)
                   {
                       count++;
                   }
                   count.Should().Be(10);
               });

                while (count < 5)
                {
                    Thread.Sleep(10);
                }

                recordYielder.AddNew(5);
                t.Wait();
            }

            [Fact]
            public void ShouldReturnRecordsImmediatelyWhenReady()
            {
                var recordYielder = new TestRecordYielder(5, 10, _output);
                var result = new StatementResult(TestRecordYielder.Keys, new FuncBasedRecordSet(() => recordYielder.Records));
                var temp = result.Take(5);
                var records = temp.ToList();
                records.Count.Should().Be(5);
            }
        }

        public class SummaryProperty
        {
            [Fact]
            public void ShouldThrowInvalidOperationExceptionWhenNotAtEnd()
            {
                var result = ResultCreator.CreateResult(1);
                result.AtEnd.Should().BeFalse();

                var ex = Xunit.Record.Exception(() => result.Summary);
                ex.Should().BeOfType<InvalidOperationException>();
            }

            [Fact]
            public void ShouldCallGetSummaryWhenGetSummaryIsNotNull()
            {
                bool getSummaryCalled = false;
                var result = ResultCreator.CreateResult(1, 0, () => { getSummaryCalled = true; return null; });

                // ReSharper disable once UnusedVariable
                var summary = result.Summary;

                getSummaryCalled.Should().BeTrue();
            }

            [Fact]
            public void ShouldReturnNullWhenGetSummaryIsNull()
            {
                var result = ResultCreator.CreateResult(1, 0);

                result.Summary.Should().BeNull();
            }

            [Fact]
            public void ShouldReturnExistingSummaryWhenSummaryHasBeenRetrieved()
            {
                int getSummaryCalled = 0;
                var result = ResultCreator.CreateResult(1, 0, () => { getSummaryCalled++; return new FakeSummary(); });

                // ReSharper disable once NotAccessedVariable
                var summary = result.Summary;
                // ReSharper disable once RedundantAssignment
                summary = result.Summary;
                getSummaryCalled.Should().Be(1);
            }
        }

        public class SingleMethod
        {
            [Fact]
            public void ShouldThrowInvalidOperationExceptionIfNoRecordFound()
            {
                var result = new StatementResult(new [] { "test" }, new ListBasedRecordSet(new List<IRecord>()));
                var ex = Xunit.Record.Exception(() => result.Single());
                ex.Should().BeOfType<InvalidOperationException>();
                // INFO: Changed message because use of Enumerable.Single for simpler implementation 
                ex.Message.Should().Be("Sequence contains no elements");
            }

            [Fact]
            public void ShouldThrowInvalidOperationExceptionIfMoreThanOneRecordFound()
            {
                var result = ResultCreator.CreateResult(1, 2);
                var ex = Xunit.Record.Exception(() => result.Single());
                ex.Should().BeOfType<InvalidOperationException>();
                // INFO: Changed message because use of Enumerable.Single for simpler implementation 
                ex.Message.Should().Be("Sequence contains more than one element");
            }

            [Fact]
            public void ShouldThrowInvalidOperationExceptionIfNotTheFistRecord()
            {
                var result = ResultCreator.CreateResult(1, 2);
                var enumerator = result.GetEnumerator();
                enumerator.MoveNext().Should().BeTrue();
                enumerator.Current.Should().NotBeNull();

                var ex = Xunit.Record.Exception(() => result.Single());
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be("The first record is already consumed.");
            }

            [Fact]
            public void ShouldReturnRecordIfSingle()
            {
                var result = ResultCreator.CreateResult(1);
                var record = result.Single();
                record.Should().NotBeNull();
                record.Keys.Count.Should().Be(1);
            }
        }

        public class PeekMethod
        {
            [Fact]
            public void ShouldReturnNextRecordWithoutMovingCurrentRecord()
            {
                var result = ResultCreator.CreateResult(1);
                result.Position.Should().Be(-1);
                var record = result.Peek();
                record.Should().NotBeNull();

                result.Position.Should().Be(-1);
                result.GetEnumerator().Current.Should().BeNull();
            }

            [Fact]
            public void ShouldReturnNullIfAtEnd()
            {
                var result = ResultCreator.CreateResult(1);
                result.Take(1).ToList();
                result.Position.Should().Be(0);
                var record = result.Peek();
                record.Should().BeNull();
            }
        }

        private class FakeSummary : IResultSummary
        {
            public Statement Statement { get; }
            public ICounters Counters { get; }
            public StatementType StatementType { get; }
            public bool HasPlan { get; }
            public bool HasProfile { get; }
            public IPlan Plan { get; }
            public IProfiledPlan Profile { get; }
            public IList<INotification> Notifications { get; }
        }
    }
}
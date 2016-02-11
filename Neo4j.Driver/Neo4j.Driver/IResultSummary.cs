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

using System.Collections.Generic;

namespace Neo4j.Driver
{
    public enum StatementType
    {
        Unknown,
        ReadOnly,
        ReadWrite,
        WriteOnly,
        SchemaWrite
    }

    public interface IResultSummary
    {
        /// Return statement that has been executed
        Statement Statement { get; }

        /// Return update statistics for the statement
        ICounters Counters { get; }

        /// Return type of statement that has been executed
        StatementType StatementType { get; }

        /// Return true if the result contained a statement plan, i.e. is the summary of a Cypher "PROFILE" or "EXPLAIN" statement
        bool HasPlan { get; }

        /// Return true if the result contained profiling information, i.e. is the summary of a Cypher "PROFILE" statement
        bool HasProfile { get; }

        /// This describes how the database will execute your statement.
        /// 
        /// Return statement plan for the executed statement if available, otherwise null
        IPlan Plan { get; }

        /// This describes how the database did execute your statement.
        /// 
        /// If the statement you executed {@link #hasProfile() was profiled}, the statement plan will contain detailed
        /// information about what each step of the plan did. That more in-depth version of the statement plan becomes
        /// available here.
        /// 
        /// Return profiled statement plan for the executed statement if available, otherwise null
        IProfiledPlan Profile { get; }

        /// A list of notifications that might arise when executing the statement.
        /// Notifications can be warnings about problematic statements or other valuable information that can be presented
        /// in a client.
        /// 
        /// Unlike failures or errors, notifications do not affect the execution of a statement.
        /// 
        /// Return a list of notifications produced while executing the statement. The list will be empty if no
        /// notifications produced while executing the statement.
        IList<INotification> Notifications { get; }
    }
}
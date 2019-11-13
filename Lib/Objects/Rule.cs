﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Types;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace AttackSurfaceAnalyzer.Objects
{

    public class Rule
    {
        public List<PLATFORM> Platforms { get; }

        public List<CHANGE_TYPE> ChangeTypes { get; }

        public string Name { get; set; }
        public string Description { get; set; }
        public ANALYSIS_RESULT_TYPE Flag { get; set; }
        public RESULT_TYPE ResultType { get; set; }

        public List<Clause> Clauses { get; set; }
    }

    public class Clause
    {
        public string Field { get; set; }
        public OPERATION Operation { get; set; }

        public List<string> Data { get; set; }

        public List<KeyValuePair<string, string>> DictData { get; set; }
    }
}

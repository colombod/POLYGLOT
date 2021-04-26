﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Polyglot.Core;
using Polyglot.CSharp;

namespace Polyglot.Interactive.Tests
{

    public class FakeTriangleExerciseValidator
    {
        private int _currentLevel = 0;

        private static FieldStructure _base = new FieldStructure(new VariableStructure("_base", DeclarationContextKind.Type, "float"), new[] { "private" });
        private static FieldStructure _height = new FieldStructure(new VariableStructure("_height", DeclarationContextKind.Type, "float"), new[] { "private" });
        private static ConstructorStructure _constructor = new(new[] { new VariableStructure("base", DeclarationContextKind.Method, "float"), new VariableStructure("height", DeclarationContextKind.Method, "float") });
        private static MethodStructure _calculateArea = new MethodStructure("calculateArea", DeclarationContextKind.Type, "float", new[] { "public" }, Array.Empty<VariableStructure>(), new MethodBodyStructure(Array.Empty<VariableStructure>()));

        private Dictionary<int, ClassStructure> _expectedResults = new()
        {
            // step 1: class declaration
            { 0, new ClassStructure("Triangle", DeclarationContextKind.TopLevel, new[] { "public" }, Array.Empty<FieldStructure>(), Array.Empty<MethodStructure>(), Array.Empty<ConstructorStructure>(), Array.Empty<ClassStructure>()) },     
            // step 2: field declaration
            { 1, new ClassStructure("Triangle", DeclarationContextKind.TopLevel, new[] { "public" }, new[] { _base, _height }, Array.Empty<MethodStructure>(), Array.Empty<ConstructorStructure>(), Array.Empty<ClassStructure>()) },          
            // step 3: constructor
            { 2, new ClassStructure("Triangle", DeclarationContextKind.TopLevel, new[] { "public" }, new[] { _base, _height }, Array.Empty<MethodStructure>(), new[] { _constructor }, Array.Empty<ClassStructure>()) },          
            // step 4: Area method
            { 3, new ClassStructure("Triangle", DeclarationContextKind.TopLevel, new[] { "public" }, new[] { _base, _height }, new[] { _calculateArea }, new[] { _constructor }, Array.Empty<ClassStructure>()) },  
        };

        public HttpResponseMessage CheckSubmission(string requestContent)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            var metrics = requestContent.ToObject<HttpContentBody>();
            var declarationsMetricObj = metrics?.data["topLevelClassesStructureMetric"];
            var declarationsMetric = declarationsMetricObj?.ToString().ToObject<IEnumerable<ClassStructure>>();

            var exerciseData = declarationsMetric?.Where(c => c.Name == "Triangle").FirstOrDefault();

            if (exerciseData is null)
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                return response;
            }

            ClassStructure expected;
            var levelExists = _expectedResults.TryGetValue(_currentLevel, out expected);

            if (levelExists && expected.Equals(exerciseData))
            {
                _currentLevel++;
            }
            response.Content = getStatus().ToBody();
            return response;
        }

        public HttpResponseMessage GetReport(string requestContent)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            response.Content = getStatus().ToBody();

            return response;
        }

        internal HttpResponseMessage AuthToken(string arg)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            var dummyAuth = new AuthenticationResponse("dummyToken", "bearer", "dummyRefresh", 9999, "dummyScope", "dummyJTI");

            response.Content = dummyAuth.ToBody();
            return response;
        }

        private GameStatus getStatus()
        {
            return new GameStatus(
                "dummyPlayerId",
                "dummyGameId",
                new GameState(new[] { new PointConcept("0", "points", _currentLevel * 10), new PointConcept("1", "gold coins", _currentLevel) }),
                new CustomData(_currentLevel.ToString(), "")
            );
        }
    }
}

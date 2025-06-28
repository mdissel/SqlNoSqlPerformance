// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
DotNetEnv.Env.TraversePath().Load();
//Console.WriteLine("SqlNoSqlPerformance");
//var noSql = new SqlNoSqlPerformance.MartenDb.NoSql();
//await noSql.InitializeAsync();
//await noSql.Insert();
//await noSql.InsertBatch();
//noSql.SelectWithIncludes();

//var sql = new SqlNoSqlPerformance.EF.EF();
//await sql.InitializeAsync();
//await sql.Insert();
//sql.SelectWithIncludes();
//await noSql.InsertBatch();

BenchmarkRunner.Run<SqlNoSqlPerformance.MartenDb.NoSql>();
BenchmarkRunner.Run<SqlNoSqlPerformance.EF.EF>();
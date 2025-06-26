using BenchmarkDotNet.Attributes;
using Bogus;
using JasperFx;
using JasperFx.CodeGeneration.Frames;
using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlNoSqlPerformance
{
	public class NoSql : IDisposable
	{

		public class Company
		{

			public long Id { get; set; }
			public string Name { get; set; }
			public List<Address> Addresses { get; set; } = new List<Address>();
			public List<long> Tags { get; set; } = new List<long>();

			public IReadOnlyCollection<long> CountryIds
			{
				get
				{
					return Addresses.Select(x => x.CountryId).Distinct().ToArray();
				}
			}
		}

		public class Address
		{

			public class AddressFaker : Faker<Address>
			{
				public AddressFaker()
				{
					RuleFor(o => o.Street, f => f.Address.StreetAddress());
					RuleFor(o => o.City, f => f.Address.City());
					RuleFor(o => o.ZipCode, f => f.Address.ZipCode());
					RuleFor(o => o.CountryId, f => f.Random.Int(1, 5));
					RuleFor(o => o.State, f => f.Address.State());

				}
			}
			public string Street { get; set; }
			public string City { get; set; }
			public string State { get; set; }
			public string ZipCode { get; set; }
			public long CountryId { get; set; }
		}

		public class Country
		{
			public long Id { get; set; }
			public string Code { get; set; }
			public string Name { get; set; }
		}

		public class Tag
		{
			public long Id { get; set; }
			public string Code { get; set; }
			public string Name { get; set; }
		}


		private readonly DocumentStore _documentStore;
		public NoSql()
		{
			_documentStore = DocumentStore.For(options =>
			{
				options.Connection(DotNetEnv.Env.GetString("CONNECTIONSTRING"));
				options.AutoCreateSchemaObjects = AutoCreate.All;
				options.DatabaseSchemaName = "public";
			});
		}


		[GlobalSetup]
		public async Task InitializeAsync()
		{
			await _documentStore.Advanced.Clean.DeleteAllDocumentsAsync();
			Bogus.Faker<Country> countryFaker = new Bogus.Faker<Country>()
				.RuleFor(o => o.Code, f => f.Address.CountryCode())
				.RuleFor(o => o.Name, f => f.Address.Country());
			await _documentStore.BulkInsertAsync(countryFaker.Generate(5));

			var tagFaker = new Bogus.Faker<Tag>()
				.RuleFor(o => o.Code, f => f.Random.AlphaNumeric(5).ToUpper())
				.RuleFor(o => o.Name, f => f.Commerce.ProductName());
			await _documentStore.BulkInsertAsync(tagFaker.Generate(50));
		}

		public void Dispose()
		{
			((IDisposable)_documentStore).Dispose();
		}

		[Benchmark]
		public async Task Insert()
		{
			var randomAddress = new Random();
			using var session = _documentStore.LightweightSession();
			for (int i = 0; i < 1000; i++)
			{
				Company company = CreateCompany(randomAddress, i);
				session.Store(company);
			}
			await session.SaveChangesAsync();
		}

		[Benchmark]
		public async Task InsertBatch()
		{
			var randomAddress = new Random();
			using var session = _documentStore.LightweightSession();
			var docs = new List<Company>();
			for (int i = 0; i < 1000; i++)
			{
				Company company = CreateCompany(randomAddress, i);
				docs.Add(company);
			}
			await _documentStore.BulkInsertAsync(docs);
		}

		[Benchmark]
		public void SelectWithIncludes()
		{
			var random = new Random();
			using var session = _documentStore.LightweightSession();
			var dict = new Dictionary<long, Country>();
			var tags = new Dictionary<long, Tag>();
			var top = random.Next(1, 1000);
			var _ = session.Query<Company>()
				.Where(x => x.Id > 0)
				.Include(dict).On(x => x.CountryIds)
				.Include(tags).On(x => x.Tags)
				.Take(top)
				.OrderBy(x => x.Name)
				.ToList();
			Console.WriteLine($"Countries: {dict.Count}");
			Console.WriteLine($"Tags: {tags.Count}");
		}



		private static Company CreateCompany(Random randomAddress, int i)
		{
			Bogus.Faker<Company> faker = new Faker<Company>()
				.RuleFor(o => o.Name, f => f.Company.CompanyName());
			var company = faker.Generate();
			for (int j = 0; j < randomAddress.Next(1, 4); j++)
			{
				company.Addresses.Add(new Address.AddressFaker().Generate());
			}
			for (int j = 0; j < randomAddress.Next(1, 6); j++)
			{
				company.Tags.Add(randomAddress.Next(1, 50));
			}
			return company;
		}
	}
}

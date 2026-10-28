// 정수 식별자의 종류 구분, 미지정 상태와 배열 인덱스 경계를 검증한다.
// 관련 기획: docs/tasks/M0-03-ids-random.md.
using System;
using System.Reflection;
using Hora.Domain.Ids;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>서로 다른 엔티티 참조가 섞이지 않는지 검증한다.</summary>
    public sealed class IdTests
    {
        /// <summary>미지정 식별자는 배열의 유효한 행 인덱스가 아니다.</summary>
        [Test]
        public void CityId_None_IsNotValid()
        {
            // Arrange
            CityId none = CityId.None;

            // Act / Assert
            Assert.That(none.Value, Is.EqualTo(-1));
            Assert.That(none.IsValid, Is.False);
            Assert.That(default(CityId).Value, Is.Zero);
            Assert.That(default(CityId).IsValid, Is.True);
        }

        /// <summary>예약된 미지정 값보다 작은 인덱스는 입력 시 차단한다.</summary>
        [Test]
        public void CityId_NegativeBelowMinusOne_Throws()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityId(-2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityId(int.MinValue));
        }

        /// <summary>모든 종류가 같은 인덱스·동등성·정렬·문화권 독립 표시 계약을 지킨다.</summary>
        [Test]
        public void Id_AllSixTypes_RespectValueContracts()
        {
            // Arrange / Act / Assert
            VerifyId(value => new ContinentId(value), ContinentId.None, "Continent");
            VerifyId(value => new NationId(value), NationId.None, "Nation");
            VerifyId(value => new CityId(value), CityId.None, "City");
            VerifyId(value => new CompanyId(value), CompanyId.None, "Company");
            VerifyId(value => new ProductId(value), ProductId.None, "Product");
            VerifyId(value => new FacilityId(value), FacilityId.None, "Facility");
        }

        /// <summary>형태가 같은 int 값도 종류가 다르면 같지 않고 암시적 변환을 제공하지 않는다.</summary>
        /// <param name="idType">검증할 식별자 타입.</param>
        [TestCase(typeof(ContinentId))]
        [TestCase(typeof(NationId))]
        [TestCase(typeof(CityId))]
        [TestCase(typeof(CompanyId))]
        [TestCase(typeof(ProductId))]
        [TestCase(typeof(FacilityId))]
        public void Id_DistinctTypes_HaveNoImplicitConversion(Type idType)
        {
            // Arrange
            var city = new CityId(3);

            // Act
            MethodInfo? conversion = idType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static);

            // Assert
            Assert.That(conversion, Is.Null);
            Assert.That(city.Equals(new CompanyId(3)), Is.False);
            Assert.That(city.Equals(3), Is.False);
            Assert.That(city.Equals(null), Is.False);
        }

        /// <summary>비교 연산자는 오버플로 없이 인덱스 순서를 표현한다.</summary>
        [Test]
        public void CityId_ComparisonOperators_OrderIndices()
        {
            // Arrange
            CityId none = CityId.None;
            var first = new CityId(0);
            var sameFirst = new CityId(0);
            var last = new CityId(int.MaxValue);

            // Act / Assert
            Assert.That(first == sameFirst, Is.True);
            Assert.That(first != last, Is.True);
            Assert.That(none < last, Is.True);
            Assert.That(last > none, Is.True);
            Assert.That(first <= sameFirst, Is.True);
            Assert.That(first >= sameFirst, Is.True);
        }

        /// <summary>서로 다른 식별자 타입에 같은 경계 검증을 적용한다.</summary>
        private static void VerifyId<T>(Func<int, T> factory, T none, string prefix)
            where T : struct, IEquatable<T>, IComparable<T>
        {
            T first = factory(0);
            T third = factory(3);
            T maximum = factory(int.MaxValue);
            Assert.That(none.Equals(factory(-1)), Is.True);
            Assert.That(first.Equals(factory(0)), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(factory(0).GetHashCode()));
            Assert.That(third.CompareTo(first), Is.Positive);
            Assert.That(none.CompareTo(maximum), Is.Negative);
            Assert.That(third.CompareTo(factory(3)), Is.Zero);
            Assert.That(third.ToString(), Is.EqualTo(prefix + "#3"));
            Assert.Throws<ArgumentOutOfRangeException>(() => factory(-2));
        }
    }
}

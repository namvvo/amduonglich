# Vietnamese lunar calendar engine

## Overview

`VietnameseLunarCalendarService` is an offline, deterministic implementation of the Vietnamese lunisolar calendar. It is in the Infrastructure project; the `LunarDate`, `LunarMonthInfo`, and `ILunarCalendarService` contract are in Domain. The service is registered as a singleton by the app's existing MAUI composition root. Its only cache is an immutable-per-year `ConcurrentDictionary`, so repeated conversions are thread-safe.

The engine computes astronomical new moons and solar terms; it does not import or depend on a Chinese calendar package or another calendar library. In particular, all astronomical events are assigned to fixed UTC+7 civil dates before the Vietnamese month rules are applied.

## New moons and Julian day numbers

The engine numbers lunations from Meeus's January 2000 epoch and evaluates the mean new-moon polynomial, periodic terms for the true phase, and the equation of time ΔT to convert the dynamical-time phase estimate to UT. The phase correction terms are expressed in days. The UTC instant is shifted by exactly seven hours when selecting the Vietnamese civil date:

`JDN = floor(JD_UT + 0.5 + 7/24)`

`DateOnly.DayNumber` is mapped to and from the integer Julian day number using the proleptic Gregorian calendar. No machine timezone, `DateTime.Now`, or daylight-saving rules participate.

The implementation uses Meeus, *Astronomical Algorithms*, 2nd edition (Willmann-Bell, 1998), Chapter 49 for new moon phases, and the standard apparent solar longitude equations (Chapter 25). This is an independent implementation of mathematical formulas; no Ho Ngoc Duc source code is copied. The ΔT piecewise polynomials follow Fred Espenak and Jean Meeus's published NASA eclipse-canon expressions. See [NASA's ΔT polynomial reference](https://eclipse.gsfc.nasa.gov/SEhelp/deltatpoly2004.html) and [NASA's eclipse canon](https://eclipse.gsfc.nasa.gov/5MCLE/5MCLE-Text10.pdf). The Meeus book is a copyrighted reference work; no third-party implementation or runtime library is bundled. The NASA formula page identifies its expressions as adapted from the Espenak and Meeus canon.

## Month 11, solar terms, and leap months

For each Gregorian December, the service locates the apparent solar longitude crossing at 270° (the winter solstice) and selects the new-moon civil date whose lunar month contains that solstice. That month is lunar month 11. The lunar year's first month begins two new moons after the previous month 11; the next year's first month is derived in the same way. This produces 12 or 13 lunar months from astronomical lunation boundaries rather than a hardcoded leap-year table.

Solar longitude crossings are refined numerically between daily samples. Their instants are converted to Vietnam civil dates at UTC+7. For a 13-month lunar year, the first month after month 11 that contains no principal solar term (longitude divisible by 30°) is marked leap and repeats the preceding month number. Month intervals include their start date and exclude the next new-moon date. This date-level rule deliberately assigns a term and a new moon on the same Vietnamese civil date to the month starting on that date.

## Supported range and boundary behavior

- Gregorian inputs: 1900-01-01 through 2100-12-31.
- Lunar month listing and direct lunar input: lunar years 1900–2100.
- Lunar-year 2100 may end in January 2101; that short spillover is accepted so the complete final supported lunar year can round-trip. Gregorian inputs in 2101 after lunar year 2100 ends are rejected.
- Gregorian dates in January before Tết may convert to lunar year `GregorianYear - 1`; the required boundary-year envelope is handled internally.
- Month lengths are computed from consecutive local new-moon dates and must be 29 or 30 days. A direct lunar date request for day 30 in a 29-day month is rejected. A recurring lunar calendar event on day 30 is skipped in years where that month has only 29 days; it is never moved to day 29.

The astronomical approximations are intended for civil calendar conversion over 1900–2100, not precision ephemeris work. Predicted ΔT values in future years and reconstructed ΔT in historical years have uncertainty; near-midnight astronomical events are inherently sensitive to model error. The conversion uses the modern proleptic Gregorian calendar across the full range and does not model historical jurisdiction-specific calendar reforms.

## Independent Vietnamese reference checks

The xUnit vectors were checked against two independently maintained Vietnamese calendar references per date. `2026-10-15` in the supplied list was incorrect: it is **6/9/2026**, not 5/9/2026.

| Gregorian date | Correct Vietnamese lunar date | Independent references |
|---|---|---|
| 2024-02-10 | 1/1/2024 | [Thư Viện Pháp Luật](https://thuvienphapluat.vn/hoi-dap-phap-luat/112024-am-la-ngay-bao-nhieu-duong-thang-1-am-lich-nam-2024-co-bao-nhieu-ngay-138016330.html), [XSKT](https://xskt.com.vn/lich-am-van-nien/thang/2-2024) |
| 2024-09-17 | 15/8/2024 | [Lịch Âm Dịch Số](https://licham.tuvidichso.com/lich-am/ngay-17-09-2024), [XSKT](https://xskt.com.vn/lich-am-van-nien/ngay/17-9-2024) |
| 2025-01-29 | 1/1/2025 | [XSKT](https://xskt.com.vn/lich-am-van-nien/ngay/29-1-2025), [Lịch Âm Dịch Số](https://licham.tuvidichso.com/lich-am/ngay-29-01-2025) |
| 2025-02-28 | 1/2/2025 | [XSKT](https://xskt.com.vn/lich-am-van-nien/ngay/28-2-2025), [Kinh tế & Đô thị](https://kinhtedothi.vn/lich-am-28-2-lich-van-nien-ngay-28-2-2025.html) |
| 2025-03-29 | 1/3/2025 | [Âm Lịch VN](https://amlich.vn/xem-ngay-tot-xau-29-03-2025), [Lịch Âm](https://licham.net/xem-lich/29-03-2025) |
| 2025-04-28 | 1/4/2025 | [Âm Lịch VN](https://amlich.vn/xem-ngay-tot-xau-28-04-2025), [Thiên Tuệ](https://thientue.vn/xem-ngay-28-thang-4-nam-2025.html) |
| 2025-10-06 | 15/8/2025 | [Lịch Âm Ngày](https://lichamngay.com/nam-2025/thang-10/lich-am-ngay-6-10-2025.html), [Lịch Âm Dương](https://lichamduong.net/lich-nam-2025/thang-10/ngay-6/) |
| 2026-02-17 | 1/1/2026 | [XSKT](https://xskt.com.vn/lich-am-van-nien/ngay/17-2-2026), [Báo Quảng Nam](https://baoquangnam.vn/am-duong-lich-ngay-17-thang-2-nam-2026.html) |
| 2026-10-15 | 6/9/2026 (corrected) | [Âm Lịch Date](https://amlich.date/doi-ngay), [Xem.com.vn](https://xem.com.vn/lich-van-nien/2026-10-15/?lvn_purpose=xuat_hanh) |

Leap-month cross-checks include [Vietnamese 2023 leap month 2](https://lichvannien365.com/thang-2-am-lich-nhuan-nam-2023-co-xau.html) and [2025 leap month 6](https://congngheviet.com/lich-van-nien/thang-6-nhuan-am-lich-2025/). The local month starts for 2025 leap month 6 (2025-07-25) are covered by unit tests.

## Tests

Run the independent engine suite with:

```powershell
dotnet test tests/VietnameseLunarCalendar.Tests/VietnameseLunarCalendar.Tests.csproj
```

The suite checks the verified date vectors, Tet boundaries, leap month distinction and placement, invalid leap/date combinations, 29/30-day months, every lunar-year month edge and continuity from 1900–2100, and every Gregorian date in the supported range round-tripping both ways.

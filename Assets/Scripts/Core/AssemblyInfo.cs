using System.Runtime.CompilerServices;

// EditMode 테스트가 Core의 internal 멤버를 검증할 수 있게 한다.
//
// 왜 필요한가: Tools/CoreStandalone은 Core 소스와 테스트를 **한 어셈블리로** 묶어
// 컴파일하므로 internal 접근이 그냥 된다. Unity는 Shmup.Core와 Shmup.Core.Tests를
// 별도 어셈블리로 컴파일하므로 막힌다 — dotnet test가 전부 통과해도 Unity 컴파일이
// 깨지고, 컴파일이 깨지면 씬 빌드와 플레이어 빌드가 **전부** 멈춘다.
//
// 대안은 internal을 public으로 올리는 것이었지만, 테스트 편의를 위해 공개 표면을
// 넓히는 것보다 테스트 어셈블리에만 열어 주는 쪽이 낫다.
[assembly: InternalsVisibleTo("Shmup.Core.Tests")]

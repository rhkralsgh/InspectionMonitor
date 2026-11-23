# InspectionMonitor

Oracle에 저장된 제조 검사 기록을 조회하고 불합격률을 막대그래프로 보여주는 WPF 데스크톱 프로그램입니다. 가상 검사 데이터로 날짜·설비별 조회를 구현하고, UCI SECOM 공개 데이터로 파일 가져오기와 주별 불합격률 집계 기능을 추가했습니다.

## 주요 기능

| 화면 | 기능 |
| --- | --- |
| 제조 검사 데이터 조회 | 날짜와 설비로 검사 기록을 조회하고, 전체 검사 건수·NG 건수·설비별 불합격률을 확인합니다. 가상 검사 데이터는 측정값이 5.0 이상 6.0 이하이면 합격으로 판정합니다. |
| 공개 데이터 (SECOM) | `secom_labels.data`를 Oracle에 저장하고, 선택한 기간의 검사 내역과 월요일 기준 주별 불합격률을 확인합니다. 목록에서 불합격 기록만 볼 수도 있습니다. |

SECOM 전체 기록을 가져오면 **총 1,567건 중 104건이 불합격**으로 집계되며, 전체 불합격률은 **6.6%**입니다.

## 기술 스택

- **화면** · C#, WPF, .NET 8
- **데이터베이스** · Oracle Database Free, ODP.NET
- **테스트** · xUnit

## 구현

- `equipment`와 `inspection_result`를 설비 ID로 연결하고, 날짜와 설비 조회 조건을 SQL 매개변수로 전달했습니다.
- SECOM에서는 같은 검사 시각이 여러 번 나올 수 있어 원본 행 번호를 기본 키로 사용했습니다. 같은 파일을 다시 가져와도 검사 기록은 중복 저장되지 않습니다.
- Oracle 조회, C# 집계, WPF 화면 표시를 분리해 판정과 집계 로직을 데이터베이스 연결 없이 테스트할 수 있도록 했습니다.

## 실행 방법

Windows와 .NET 8 SDK가 필요합니다. 앱은 `127.0.0.1:1521/FREEPDB1`의 Oracle에 `inspection_app` 계정으로 접속합니다. 처음 실행한다면 아래 Oracle 초기 설정을 먼저 진행합니다.

저장소 폴더에서 PowerShell을 열고 실행합니다.

```powershell
$oraclePassword = Read-Host 'inspection_app 비밀번호' -AsSecureString
$env:INSPECTION_DB_PASSWORD = [System.Net.NetworkCredential]::new('', $oraclePassword).Password

try {
    dotnet run --project .\InspectionMonitor.csproj
} finally {
    Remove-Item Env:\INSPECTION_DB_PASSWORD -ErrorAction SilentlyContinue
}
```

<details>
<summary>Oracle 초기 설정 (Podman)</summary>

### 1. Oracle Database Free 실행

Podman이 실행 중인 상태에서 아래 명령을 입력합니다. 이미 `inspection-oracle` 컨테이너를 설정했다면 `podman start inspection-oracle`로 시작합니다.

```powershell
podman run -d --name inspection-oracle -p 127.0.0.1:1521:1521 -v inspection-oracle-data:/opt/oracle/oradata container-registry.oracle.com/database/free:latest-lite
```

### 2. 앱 계정 생성

데이터베이스가 준비되면 아래 내용을 `create-inspection-user.sql`로 저장합니다. 처음 설정할 때 한 번 실행하며, 비밀번호는 SQL*Plus가 묻는 시점에 입력합니다.

```sql
WHENEVER SQLERROR EXIT FAILURE
SET VERIFY OFF
ALTER SESSION SET CONTAINER = FREEPDB1;

COLUMN app_tablespace NEW_VALUE app_tablespace NOPRINT
SELECT property_value AS app_tablespace
FROM database_properties
WHERE property_name = 'DEFAULT_PERMANENT_TABLESPACE';

ACCEPT app_password CHAR PROMPT 'Inspection app password: ' HIDE
CREATE USER inspection_app IDENTIFIED BY "&app_password"
    DEFAULT TABLESPACE "&app_tablespace"
    QUOTA 20M ON "&app_tablespace";
GRANT CREATE SESSION, CREATE TABLE TO inspection_app;
UNDEFINE app_password
EXIT
```

```powershell
podman cp create-inspection-user.sql inspection-oracle:/tmp/create-inspection-user.sql
podman exec -it --user oracle inspection-oracle sqlplus -L / as sysdba "@/tmp/create-inspection-user.sql"
```

### 3. 가상 검사 데이터 준비

아래 내용을 `inspection-sample.sql`로 저장합니다. 새로 생성한 `inspection_app` 계정에서 한 번 실행하면 한국 시간 기준으로 설정한 날의 샘플을 메인 화면에서 조회할 수 있습니다.

```sql
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

CREATE TABLE equipment (
    equipment_id NUMBER(9) PRIMARY KEY,
    equipment_name NVARCHAR2(30) NOT NULL UNIQUE
);

CREATE TABLE inspection_result (
    inspection_id NUMBER(9) PRIMARY KEY,
    equipment_id NUMBER(9) NOT NULL REFERENCES equipment(equipment_id),
    inspected_at DATE NOT NULL,
    measured_value NUMBER(8,2) NOT NULL
);

INSERT ALL
    INTO equipment VALUES (1, UNISTR('\C124\BE44 A'))
    INTO equipment VALUES (2, UNISTR('\C124\BE44 B'))
    INTO equipment VALUES (3, UNISTR('\C124\BE44 C'))
SELECT 1 FROM dual;

INSERT ALL
    INTO inspection_result VALUES (1, 1, sample_day + 9/24, 5.4)
    INTO inspection_result VALUES (2, 1, sample_day + 10/24, 6.2)
    INTO inspection_result VALUES (3, 2, sample_day + 11/24, 4.9)
    INTO inspection_result VALUES (4, 2, sample_day + 12/24, 5.8)
    INTO inspection_result VALUES (5, 3, sample_day + 13/24, 5.5)
SELECT TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Seoul' AS DATE)) AS sample_day
FROM dual;

COMMIT;
EXIT
```

```powershell
podman cp inspection-sample.sql inspection-oracle:/tmp/inspection-sample.sql
podman exec -it --user oracle inspection-oracle sqlplus -L "inspection_app@localhost:1521/FREEPDB1" "@/tmp/inspection-sample.sql"
```

</details>

### SECOM 가져오기

1. [UCI SECOM 데이터 페이지](https://archive.ics.uci.edu/dataset/179/secom)에서 데이터를 내려받고 압축을 풉니다.
2. 앱에서 **공개 데이터 (SECOM) → 판정 파일 가져오기**를 누르고 `secom_labels.data`를 선택합니다.
3. 가져오기가 끝나면 전체 기간의 검사 내역과 주별 불합격률을 확인할 수 있습니다.

## 테스트

```powershell
dotnet test .\InspectionMonitor.Tests\InspectionMonitor.Tests.csproj
```

판정 경계값, 빈 조회 결과, 설비별 불합격률, 중복 검사 시각, 잘못된 판정 파일 형식, 일요일과 월요일 사이의 주차 구분을 검증합니다.

## 데이터 출처

McCann, M. & Johnston, A. (2008). *SECOM*. UCI Machine Learning Repository. CC BY 4.0. [DOI](https://doi.org/10.24432/C54305)

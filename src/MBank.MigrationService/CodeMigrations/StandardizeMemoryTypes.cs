using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.MigrationService.CodeMigrations.Engine;
using Microsoft.EntityFrameworkCore;

namespace MBank.MigrationService.CodeMigrations;

[CodeMigration("20260518013000_standardize_memory_types")]
internal sealed class StandardizeMemoryTypes(
    MemoryDbContext db,
    ILogger<StandardizeMemoryTypes> logger) : ICodeMigration
{
    internal static IReadOnlyDictionary<MemoryId, MemoryType> StandardizedTypes { get; } =
        new Dictionary<MemoryId, MemoryType>
        {
            [new MemoryId(new Guid("019e0458-048b-793d-9b7c-7c0db39868b2"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e045a-3cb8-7b7e-8106-0ad8f41e5d31"))] = MemoryType.ProductStrategy,
            [new MemoryId(new Guid("019e045a-3f5d-7973-b881-910e72bba1ee"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e045a-3fea-7d94-95d6-254aa5ff857f"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e045a-40b7-7a56-b060-d880babeefbc"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e045a-4185-7e14-a358-74d3c5f8eec7"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e045a-4251-72e1-8542-939f6982bfab"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e045a-431e-7b30-bf57-1001742f878e"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-43ec-73e6-a18d-9c4fd29380b4"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-44b8-77d8-8643-f9f0226f35e0"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-4585-7bfe-8f71-8eab3ad236ba"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-4651-7ad6-ab0f-b903eba44774"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-48b9-7fb5-999a-991437183979"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4949-703a-a559-be7dddce0c92"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-49ec-7b5d-bb32-e268cfc98739"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4a63-7202-864a-e8a14a2ccccc"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4b84-78a9-8196-272c054deafd"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4c28-7947-ba56-7416decc1044"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4ca9-7f7f-9ff6-518638c872f2"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-4d1d-73f9-b14c-247e4d167390"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-4deb-786f-b285-3b196b3e7809"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4eb8-7e78-b59f-b3fb8f5e36b3"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-4f85-7df3-81d9-f8e23b039097"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-502c-72dd-8931-4f7b3ff6410d"))] = MemoryType.TechnicalRisk,
            [new MemoryId(new Guid("019e045a-50a5-72a3-8da7-a37416c41ab9"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-512c-710f-becc-5db4d9fc0e77"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e045a-51ec-7edc-8c99-bdc1ecd88376"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e045a-52b8-78af-9eba-a3eb11884786"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e045a-5386-7f78-974a-d7f657675608"))] = MemoryType.TechnicalRisk,
            [new MemoryId(new Guid("019e045a-5451-7638-86e9-465dd1b75f55"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e045a-551e-78fb-82ff-66393c39da66"))] = MemoryType.CollaborationPreference,
            [new MemoryId(new Guid("019e045a-5596-73f3-9c80-10f915ce3454"))] = MemoryType.CollaborationPreference,
            [new MemoryId(new Guid("019e0480-6974-7e25-89e2-01853a06f286"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e0480-6d7b-74ec-9eac-7660c4ab7dd3"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e08b6-740d-7bc6-990a-075d91a3880d"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e08c8-44d6-7a2d-aad6-e0bd41aa2e65"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e0919-13ba-735b-bae2-61038f331323"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e0919-1485-7c31-b887-5b7e60f809e8"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0921-ab2b-7b12-94c2-59afa2fcb993"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e0921-abfd-7e62-aa17-b395dcb49007"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e0955-4ca2-78e9-b99e-927fd3800be7"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0955-4d2d-7f11-9210-2dcb7c9b22eb"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e096e-b402-7779-a52f-fe90bcaa0856"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e096e-b720-753a-aad3-8677e368f8b3"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0975-53a8-7d35-a1cf-d42185cae0e6"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e097c-59ce-7d71-bfa4-443a081b96b1"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e097d-a973-7fe8-95db-d74e3e9170a0"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e0982-77f7-777a-9225-045460101c87"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e0987-7ecf-71bc-b832-11c61f090872"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e0988-706f-769d-af3d-8c7ebe301236"))] = MemoryType.TechnicalRisk,
            [new MemoryId(new Guid("019e098d-8292-79a4-b7dd-639a15f905b8"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e098d-834b-779b-9ac0-239dd516ef3c"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e098e-08dc-7a3a-a81f-0e091c706f75"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e098e-096f-70b8-9b1f-fdd8b9246ce0"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0cb7-fd59-7f69-baf6-363de2680359"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e0cb7-fdf1-7abf-beb4-df2fda27a99f"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e0cb9-b14d-7419-9430-74eaba264506"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0d45-a9cd-7e34-8777-d627a816f3ae"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e0d45-aa68-7e75-abc8-adf7238ce6af"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0d49-06b7-7314-9da8-5b2d60fb9fad"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e0e8e-037c-7ec4-9260-b06772128426"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e0ec0-f812-7b64-bb61-de7fa7ce6814"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e0ed4-dfac-7238-9d07-9902597cbd7b"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e0ed4-e112-797b-b41b-f1ceabc6a28b"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e0ed4-e4ad-7f07-b933-8feba99b1865"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e1212-6baa-7dba-8088-fb702ae8f185"))] = MemoryType.OperationalStatus,
            [new MemoryId(new Guid("019e1212-6cbe-71b2-a0f6-72d4f5b64408"))] = MemoryType.ProductStrategy,
            [new MemoryId(new Guid("019e1212-6e08-75dc-8ae0-d38720160600"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e1212-6ebf-7fdc-ba2b-462519116e77"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e1212-6f8c-7572-81ac-c6603d48e85e"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e1212-7057-75ad-9da5-e3f1596ca3e3"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e1212-7119-7b23-a728-a51a24eed85b"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e1212-71a9-72f1-871b-4e3f8c8659aa"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e1212-7256-70e9-b945-9be4aa02324e"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e1212-732e-7a21-80ec-15702536ff1f"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e1212-73be-7362-9ca1-4c840329fa24"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e1212-7459-7920-a951-ff4cd9aa14b3"))] = MemoryType.OperationalStatus,
            [new MemoryId(new Guid("019e1212-7529-714f-ad80-20d57d80058d"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e1212-75bf-7a39-8cea-df2e78f594aa"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e1212-7657-72bb-b1d9-9e621b0be9f9"))] = MemoryType.ProductRisk,
            [new MemoryId(new Guid("019e1212-7719-7d40-97dc-53531274afee"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e1212-77d4-78e5-b2dd-7969a8249810"))] = MemoryType.ProductOpenQuestion,
            [new MemoryId(new Guid("019e1212-7890-7431-90e5-2c67262a6866"))] = MemoryType.Note,
            [new MemoryId(new Guid("019e1215-1711-7f4f-8d63-af979189eec9"))] = MemoryType.AgentInstruction,
            [new MemoryId(new Guid("019e1271-c330-7615-bc78-58dd1174558a"))] = MemoryType.ProductReasoning,
            [new MemoryId(new Guid("019e1280-3c33-7cbb-a8e8-feddad569a52"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e1281-ca86-7129-b6c9-0191da325cfd"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e1289-4aa8-7299-8501-ea4cd7f5e3af"))] = MemoryType.ProductOpenQuestion,
            [new MemoryId(new Guid("019e128f-1f9d-79a5-bd1c-a473bebce8dd"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e1294-5868-7cae-9113-16687247b022"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e12bc-f831-7b63-9840-95d5747114a8"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e12bc-f8f4-7cf8-88b5-5c37ef44c4d6"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e12e5-1896-7a90-acfc-f60f4774caf1"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e12ed-cccf-7400-95db-1155d04eeb02"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e12ed-cd9c-7332-b963-6c45f2e2f6cd"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e12f4-04dc-7dc6-a301-75ce97b96e1b"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e12f5-84ce-7d4a-86ae-ab2951adac79"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e12f9-b2b5-78ea-aed9-b1a40f3f567c"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e1858-9339-7f99-a787-6df4f580a0be"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e18cd-d1cd-7eed-a004-10724b645e2b"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e18d2-691c-75c1-9bd6-9803187fe9e4"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e18d4-c1dc-778a-b7e5-7c0bd6d21740"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e18d4-c29e-730e-9b57-78ef7598cf85"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e2377-faaf-72bc-b4f6-3df52807f0cb"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e237f-82dc-7e3b-b502-15f9b77cd0f5"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e2382-cab5-7ceb-84b2-b388651b0519"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2382-ccbc-79d5-9ad1-ebaa3af04983"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e2386-c328-7982-a7a1-ad840ef26f10"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e2386-c39e-7f69-9ec1-54a5f69c3257"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e2387-dbcc-7298-89ac-b0bcc2075b04"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e238a-ba5b-7c24-93fc-c8e224b96dc0"))] = MemoryType.ProductStrategy,
            [new MemoryId(new Guid("019e238e-505d-736d-af3c-4637949d99d2"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2393-91ce-740e-a7ee-515c364e1188"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2393-9619-718f-aa43-65a60481eb2e"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2393-968d-7168-8ea0-23c0f07d193d"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2393-96fa-763c-bba9-70a5f0c2385e"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e2393-9767-7c9a-ae08-729dd41103de"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2397-6cdf-79cb-af24-26132efd5ffd"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2399-5a2d-71cd-825f-f1a6f610aa0b"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e23a0-4e45-7089-bc97-fd2ae8b05afc"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e23a4-93d2-7846-9058-e634c50d454c"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e23af-b8eb-7aa6-b586-b546c487dea2"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e23ba-08f7-735d-b764-10573bebfc06"))] = MemoryType.ProductRisk,
            [new MemoryId(new Guid("019e23ba-2f93-7177-b3e7-a196a96137c0"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e23ba-f625-7359-aa2e-d7646c992cf7"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e23bd-c229-7456-b10b-a107d3d7d21e"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e23c0-82dd-73e7-b5f0-8904bc526e76"))] = MemoryType.ProductRisk,
            [new MemoryId(new Guid("019e23c3-4d25-7ef1-b810-6ac9d33ad380"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e26cf-ef1c-7c71-9a5c-e766dcfb7d86"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e26cf-f4ae-7bc9-9950-bd41f8445fd1"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e26cf-f54b-7036-899a-6a98629f1bc5"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e26e3-eebf-79ff-9f99-86c82ecc34ae"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e27bc-4a5a-7718-afce-76e8a1dd127e"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e27cc-e38e-78b3-9849-d9559c8c2f28"))] = MemoryType.TechnicalDecision,
            [new MemoryId(new Guid("019e2844-f5b5-7830-ad9b-d8c6ffc29538"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e2844-f674-74f7-b7e2-cb7331254b9e"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e2863-9fa5-78df-a23b-5169fd7ec4c5"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e2866-5b0a-719f-8b81-6e6e838caa98"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e2866-ffe8-7f02-a700-25e160162ab2"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e286f-9e1a-706b-a8de-ba92ea3a7d28"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e2870-2067-7fd8-8174-392033dc0f8e"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e287b-93f6-7678-953c-e70f9065091e"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e287b-94a1-7fe6-b711-8150be311502"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e28be-0eed-7234-86c8-84859a416ed0"))] = MemoryType.OperationalStatus,
            [new MemoryId(new Guid("019e28c0-adf3-703a-9453-622a251a5220"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e2bc1-4fc7-7a40-b0b1-3e3abc224983"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2c2d-c6de-7688-9628-b56e82231c38"))] = MemoryType.TechnicalPreference,
            [new MemoryId(new Guid("019e2c8b-58aa-7237-8d66-53ebd16480ac"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e2d22-5658-7960-98cd-6c093313a9cc"))] = MemoryType.OperationalStatus,
            [new MemoryId(new Guid("019e2d26-04f9-7b5c-80d5-fcaa920272d7"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e2d26-0577-7e79-ac22-b333eefba6a1"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e2d26-05fc-79cb-8c04-7fbb524e7dba"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e2d26-06df-7858-9c42-cc4b5919ac54"))] = MemoryType.UserPreference,
            [new MemoryId(new Guid("019e2d26-0769-7ae4-ac24-b2b202a83771"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e2d26-5c9d-722a-aba0-f76f1fb345c0"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e2d26-5d18-7ee1-aa68-e695086e8a27"))] = MemoryType.ProductRisk,
            [new MemoryId(new Guid("019e2d2f-8dba-7a52-85ef-e2d6cea2b4de"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e2d43-6f27-7707-89e0-8f05eb3a139e"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2d46-7a07-7d85-af75-cad75697e616"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e2d46-7a83-7258-9f9f-a1b46bf89906"))] = MemoryType.ProductRisk,
            [new MemoryId(new Guid("019e2d50-12dc-7ef6-881f-4624e85fd77c"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e2d50-1366-7c51-b5e6-a03367ecfdc8"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e30f7-38a1-7af5-908e-f48ed31ac801"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e30f9-3524-7b2b-a972-2a96023cc58f"))] = MemoryType.ValidationResult,
            [new MemoryId(new Guid("019e3121-484d-79db-bb3d-ac8d634103eb"))] = MemoryType.CollaborationPreference,
            [new MemoryId(new Guid("019e3124-fce6-7812-95aa-9cb0143942b2"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3124-fed0-79d6-9e55-5c34bf7fa763"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e3133-4118-7fd1-8fef-ffaba2de6997"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e313e-f395-7541-be71-f2669e0f4428"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3169-cb82-74ab-97a9-f406f0a4053d"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e32a1-14e4-79ca-8814-6a19a1904d93"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e32b5-6b3d-7092-8631-7c35837f346c"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e32b6-c341-7bee-bf07-fc2b5d418736"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e32d4-9b1b-71a4-aecb-bab1bc52d0b5"))] = MemoryType.ProductRequirement,
            [new MemoryId(new Guid("019e32d8-36a7-7491-b0e5-3c5880075f16"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e32da-8a8a-7046-85b5-b97ccad889d1"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e32df-991c-753a-9d0f-8474558b28a6"))] = MemoryType.OperationalProcedure,
            [new MemoryId(new Guid("019e32e1-ce3a-7f02-878a-6d7a4d68f5b2"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e32e4-4667-7c7b-b9ba-3ef998362243"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e330f-bf8e-74b7-9bde-c724cbba43ef"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e330f-c09e-77a3-b2b3-47370d6e6ecd"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e3326-7beb-77e2-a7d2-678b5ac9ebc5"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35ab-780d-7542-ae30-99bd6be9694c"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e35ab-789c-7195-b041-57793296e591"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35ac-4edb-70c9-b802-70adcb8f62d2"))] = MemoryType.TechnicalConvention,
            [new MemoryId(new Guid("019e35be-e21d-7bb7-bd40-d37ea59d5fa8"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35bf-db48-7ca2-af02-47da708c375b"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35bf-dc28-71f2-909c-a223bf4ec002"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e35c1-6298-7369-a5c4-006cf634bd71"))] = MemoryType.AgentInstruction,
            [new MemoryId(new Guid("019e35c4-595b-785a-9ac4-810da0c10c17"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35c4-59de-7d71-a432-14c07358f18a"))] = MemoryType.TechnicalRule,
            [new MemoryId(new Guid("019e35c8-069a-7c7a-86c5-c80bfbf526dd"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e35fb-e7ca-7839-92bb-413988554906"))] = MemoryType.AgentInstruction,
            [new MemoryId(new Guid("019e3714-a6db-7476-af72-09d140142f9f"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e3721-10ac-718b-9b50-ef118704140e"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3726-987f-765a-9197-9f24af8e13a9"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3728-7cdd-7977-820e-aa31fcb583b8"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e37ec-b224-7580-9aae-162bcb32d554"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e37ec-b7d7-7c0c-b0bd-a16ab9ad7417"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e37fb-980a-788d-bee9-d3f13ff0829f"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3802-d107-79b8-8087-56b64ee3903b"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3806-93e3-76b2-90d7-c1235d6420ed"))] = MemoryType.TechnicalRule,
            [new MemoryId(new Guid("019e3811-b00d-7a64-8995-6897d4f386d8"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3821-f1c6-7f14-a6fd-1e036741346e"))] = MemoryType.ProductDecision,
            [new MemoryId(new Guid("019e3821-f242-76a3-a6f1-148084e953df"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3840-147e-7486-bd9a-f8eeed1cf7e2"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3847-4833-726c-8770-a4ee9a036ea5"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e385d-b2a6-7d74-ba3f-65cd0f38e386"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3863-97e5-7e5d-835e-305f8f53491b"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e3873-2ac9-7573-ab8c-d9291194d6d4"))] = MemoryType.ImplementationResult,
            [new MemoryId(new Guid("019e387a-63b6-7bea-96f5-574f31e86f82"))] = MemoryType.ProductConstraint,
            [new MemoryId(new Guid("019e387b-a3f2-79dc-9d6f-cc882ca01b4d"))] = MemoryType.ProductConstraint,
        };

    public async Task RunAsync()
    {
        var memories = await db.Memories.ToListAsync();

        if (memories.Count == 0)
        {
            return;
        }

        EnsureExpectedSnapshot(memories);

        var updatedCount = 0;

        foreach (var memory in memories)
        {
            var memoryType = StandardizedTypes[memory.Id];

            if (memory.Type == memoryType.Value)
            {
                continue;
            }

            memory.ChangeType(memoryType);
            updatedCount++;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Standardized {UpdatedMemoryCount} of {InspectedMemoryCount} memory type values.",
            updatedCount,
            memories.Count);
    }

    private static void EnsureExpectedSnapshot(IReadOnlyList<MemoryEntry> memories)
    {
        var actualIds = memories.Select(memory => memory.Id).ToHashSet();
        var missingIds = StandardizedTypes.Keys.Except(actualIds).ToArray();
        var unexpectedIds = actualIds.Except(StandardizedTypes.Keys).ToArray();

        if (missingIds.Length == 0 && unexpectedIds.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production memory type standardization mapping does not match the current database snapshot. "
            + $"Missing mapped IDs: {FormatIds(missingIds)}. Unexpected database IDs: {FormatIds(unexpectedIds)}.");
    }

    private static string FormatIds(IReadOnlyList<MemoryId> ids)
    {
        return ids.Count == 0
            ? "(none)"
            : string.Join(", ", ids.Select(id => id.Value));
    }
}

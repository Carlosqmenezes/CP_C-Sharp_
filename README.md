## ExpenseHub

API REST de reembolsos com ASP.NET Core, Identity, Entity Framework Core, SQLite e autenticação bearer (JWT).

### Requisitos
.NET SDK 10 e `dotnet tool install --global dotnet-ef`.

### Banco de dados
Provider:  O arquivo `expensehub.db` é criado automaticamente e as migrations são aplicadas ao iniciar a aplicação. O banco local não é versionado. Para gerar uma nova migration:

```powershell
cd sources
dotnet ef migrations add NomeDaMigration --project ExpenseHub.Api --output-dir Infrastructure/Migrations
```

### Configuração de segredos
Os segredos ficam fora do repositório (user-secrets ou variáveis de ambiente):

```powershell
cd sources
dotnet user-secrets set "Jwt:Key" "<chave aleatória com 32+ caracteres>" --project ExpenseHub.Api
dotnet user-secrets set "Seed:AdminEmail" "<email do admin>" --project ExpenseHub.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha forte>" --project ExpenseHub.Api
```

Na primeira execução o seed cria as roles Admin, Employee, Approver, Finance e Auditor e apenas a conta Admin. O seed é idempotente.

### Executar e testar
```powershell
dotnet run --project ExpenseHub.Api
dotnet test ExpenseHub.slnx
```
Os testes unitários usam EF Core InMemory e não dependem do provider SQLite.

### Roles
O cadastro (`POST /register`) nunca aceita role. O Admin atribui e remove roles em `PUT /api/admin/users/{id}/roles`. **Depois de alterar roles, o usuário precisa fazer login novamente**, porque as roles vêm do token.

### Decisões de autorização
- Leitura fora do escopo retorna 404.
- Em transições (aprovar, reprovar, pagar): reembolso inexistente ou rascunho de outro usuário retorna 404; o próprio dono retorna 403; estado incompatível ou repetido retorna 409, sem histórico duplicado.
- Remover a própria role Admin retorna 400.
- Admin não tem acesso funcional a reembolsos.

### Matriz de autorização

| Operação | Employee | Approver | Finance | Auditor | Admin |
|---|:---:|:---:|:---:|:---:|:---:|
| Registrar e login | público | público | público | público | público |
| Listar usuários e alterar roles | não | não | não | não | sim |
| Criar e editar rascunho | sim (próprio) | não | não | não | não |
| Enviar | sim (próprio) | não | não | não | não |
| Listar e consultar | próprios | Submitted | Approved/Paid | todos | não |
| Aprovar e reprovar | não | sim (não próprio) | não | não | não |
| Pagar | não | não | sim (não próprio) | não | não |
| Histórico | próprio | visível | visível | todos | não |

Roles acumulam permissões, mas ninguém aprova ou paga o próprio reembolso.

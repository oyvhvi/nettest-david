To run on local docker, build it

docker build -t atn062024:latest .

Then start a postgres db (attach a volume if you like):

docker run -d -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=mysecretpassword -p 5432:5432 --name some-postgres postgres

Create a network (could also use docker compose for all this instead)

docker network create networkatn
docker network connect networkatn some-postgres

Check the IP of some-postgres container

docker network inspect networkatn

Then connect to the postgres db and run the statements in SharedResources/sql/create_schema.sql.

Then run the built image. A connstring to the docker postgres should be specified.
Use the IP from above as the host

docker run -d -e Secrets__pgsql_connstring='User ID=postgres;Password=mysecretpassword;Host=POSTGRES_CONTAINER_IP_HERE;Port=5432;Database=postgres;Pooling=true;Min Pool Size=0;Max Pool Size=10;' -p 8080:8080 --name atntest atn062024:latest
docker network connect networkatn atntest

---

Regarding tests: Make sure to have docker running because this uses testcontainers.
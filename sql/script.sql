-- auto-generated definition
create table application_status
(
    id               serial not null
        constraint application_status_pkey
            primary key,
    name             text,
    description      text,
    code             text,
    created_at       timestamp,
    updated_at       timestamp,
    created_by       integer,
    updated_by       integer,
    name_kg          text,
    status_color     text,
    description_kg   text,
    text_color       text,
    background_color text,
    type_id INT REFERENCES contact_types(id),
);

alter table application_status
    owner to postgres;

CREATE TABLE contacts (
    id INT PRIMARY KEY,
    type_id INT REFERENCES contact_types(id),
    value VARCHAR(255) NOT NULL
);


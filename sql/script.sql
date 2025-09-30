create table organization
(
    id                serial
        primary key,
    organization_code text                 not null
        unique,
    name              text,
    short_name        text,
    organization_type text,
    inn               text,
    address           text,
    phone             text,
    email             text,
    is_active         boolean default true not null,
    created_at        timestamp with time zone,
    updated_at        timestamp with time zone,
    created_by        integer,
    updated_by        integer
);

comment on table organization is 'Organizations that receive approval requests';

alter table organization
    owner to postgres;

create index idx_organization_code
    on organization (organization_code);

create index idx_organization_type
    on organization (organization_type);

create index idx_organization_is_active
    on organization (is_active);

create table role
(
    id          serial
        primary key,
    role_code   text                  not null
        unique,
    name        text,
    description text,
    is_system   boolean default false not null,
    created_at  timestamp with time zone,
    updated_at  timestamp with time zone,
    created_by  integer,
    updated_by  integer
);

comment on table role is 'User roles in the system';

alter table role
    owner to postgres;

create table permission
(
    id              serial
        primary key,
    permission_code text not null
        unique,
    name            text,
    description     text,
    module          text,
    created_at      timestamp with time zone,
    updated_at      timestamp with time zone,
    created_by      integer,
    updated_by      integer
);

comment on table permission is 'System permissions';

alter table permission
    owner to postgres;

create table role_permission
(
    id            serial
        primary key,
    role_id       integer not null
        references role
            on delete cascade,
    permission_id integer not null
        references permission
            on delete cascade,
    created_at    timestamp with time zone,
    updated_at    timestamp with time zone,
    created_by    integer,
    updated_by    integer,
    unique (role_id, permission_id)
);

comment on table role_permission is 'Role to permission mappings';

alter table role_permission
    owner to postgres;

create table user_account
(
    id                 serial
        primary key,
    organization_id    integer               not null
        references organization
            on delete cascade,
    username           text                  not null
        unique,
    email              text,
    full_name          text,
    position           text,
    phone              text,
    password_hash      text,
    has_signing_rights boolean default false not null,
    is_active          boolean default true  not null,
    last_login         timestamp with time zone,
    created_at         timestamp with time zone,
    updated_at         timestamp with time zone,
    created_by         integer,
    updated_by         integer
);

comment on table user_account is 'System users';

alter table user_account
    owner to postgres;

create index idx_user_organization_id
    on user_account (organization_id);

create index idx_user_username
    on user_account (username);

create index idx_user_email
    on user_account (email);

create index idx_user_is_active
    on user_account (is_active);

create table user_role
(
    id          serial
        primary key,
    user_id     integer                                not null
        references user_account
            on delete cascade,
    role_id     integer                                not null
        references role
            on delete cascade,
    assigned_at timestamp with time zone default now() not null,
    assigned_by integer,
    created_at  timestamp with time zone,
    updated_at  timestamp with time zone,
    created_by  integer,
    updated_by  integer,
    unique (user_id, role_id)
);

comment on table user_role is 'User to role assignments';

alter table user_role
    owner to postgres;

create table document_type
(
    id          serial
        primary key,
    code        text                  not null
        unique,
    name        text,
    description text,
    is_required boolean default false not null,
    sort_order  integer,
    is_active   boolean default true  not null,
    created_at  timestamp with time zone,
    updated_at  timestamp with time zone,
    created_by  integer,
    updated_by  integer
);

comment on table document_type is 'Types of documents';

alter table document_type
    owner to postgres;

create table status
(
    id          serial
        primary key,
    code        text not null
        unique,
    name        text,
    description text,
    color       text,
    sort_order  integer,
    created_at  timestamp with time zone,
    updated_at  timestamp with time zone,
    created_by  integer,
    updated_by  integer
);

comment on table status is 'Request statuses';

alter table status
    owner to postgres;

create table approval_request
(
    id                     serial
        primary key,
    bga_application_number text,
    organization_id        integer not null
        references organization
            on delete cascade,
    applicant_name         text,
    approval_type          text,
    current_status         text,
    priority               text,
    operator_id            integer
                                   references user_account
                                       on delete set null,
    received_date          timestamp with time zone,
    deadline_date          timestamp with time zone,
    completed_date         timestamp with time zone,
    created_at             timestamp with time zone,
    updated_at             timestamp with time zone,
    created_by             integer,
    updated_by             integer
);

comment on table approval_request is 'Approval requests from BGA system';

alter table approval_request
    owner to postgres;

create index idx_approval_request_organization_id
    on approval_request (organization_id);

create index idx_approval_request_operator_id
    on approval_request (operator_id);

create index idx_approval_request_current_status
    on approval_request (current_status);

create index idx_approval_request_received_date
    on approval_request (received_date);

create index idx_approval_request_deadline_date
    on approval_request (deadline_date);

create table incoming_document
(
    id                  serial
        primary key,
    approval_request_id integer not null
        references approval_request
            on delete cascade,
    document_type_id    integer
                                references document_type
                                    on delete set null,
    name                text,
    file_name           text,
    file_path           text,
    file_size           bigint,
    mime_type           text,
    file_hash           text,
    bga_document_number text,
    bga_document_date   timestamp with time zone,
    received_from       integer,
    received_at         timestamp with time zone,
    created_at          timestamp with time zone,
    updated_at          timestamp with time zone,
    created_by          integer,
    updated_by          integer
);

comment on table incoming_document is 'Documents received with approval requests';

alter table incoming_document
    owner to postgres;

create index idx_incoming_document_approval_request_id
    on incoming_document (approval_request_id);

create index idx_incoming_document_document_type_id
    on incoming_document (document_type_id);

create table outgoing_document
(
    id                   serial
        primary key,
    approval_request_id  integer               not null
        references approval_request
            on delete cascade,
    incoming_document_id integer
                                               references incoming_document
                                                   on delete set null,
    document_type_id     integer
                                               references document_type
                                                   on delete set null,
    name                 text,
    file_name            text,
    file_path            text,
    file_size            bigint,
    mime_type            text,
    file_hash            text,
    document_number      text,
    document_date        timestamp with time zone,
    version              integer default 1     not null,
    prepared_by          integer
                                               references user_account
                                                   on delete set null,
    prepared_at          timestamp with time zone,
    signed_by            integer
                                               references user_account
                                                   on delete set null,
    signed_at            timestamp with time zone,
    is_final             boolean default false not null,
    sent_to_bga          boolean default false not null,
    sent_at              timestamp with time zone,
    created_at           timestamp with time zone,
    updated_at           timestamp with time zone,
    created_by           integer,
    updated_by           integer
);

comment on table outgoing_document is 'Response documents created by organization';

alter table outgoing_document
    owner to postgres;

create index idx_outgoing_document_approval_request_id
    on outgoing_document (approval_request_id);

create index idx_outgoing_document_incoming_document_id
    on outgoing_document (incoming_document_id);

create index idx_outgoing_document_prepared_by
    on outgoing_document (prepared_by);

create index idx_outgoing_document_signed_by
    on outgoing_document (signed_by);

create table outgoing_document_version
(
    id                   serial
        primary key,
    outgoing_document_id integer not null
        references outgoing_document
            on delete cascade,
    version_number       integer not null,
    file_name            text,
    file_path            text,
    file_size            bigint,
    file_hash            text,
    change_reason        text,
    created_at           timestamp with time zone,
    updated_at           timestamp with time zone,
    created_by           integer,
    updated_by           integer
);

comment on table outgoing_document_version is 'Document version history';

alter table outgoing_document_version
    owner to postgres;

create table conclusion
(
    id                  serial
        primary key,
    approval_request_id integer               not null
        references approval_request
            on delete cascade,
    conclusion_number   text,
    decision_type       text,
    conclusion_text     text,
    conditions          text,
    main_document_id    integer
                                              references outgoing_document
                                                  on delete set null,
    prepared_by         integer
                                              references user_account
                                                  on delete set null,
    prepared_at         timestamp with time zone,
    signed_by           integer
                                              references user_account
                                                  on delete set null,
    signed_at           timestamp with time zone,
    sent_to_bga         boolean default false not null,
    sent_at             timestamp with time zone,
    created_at          timestamp with time zone,
    updated_at          timestamp with time zone,
    created_by          integer,
    updated_by          integer
);

comment on table conclusion is 'Final conclusions for approval requests';

alter table conclusion
    owner to postgres;

create index idx_conclusion_approval_request_id
    on conclusion (approval_request_id);

create index idx_conclusion_main_document_id
    on conclusion (main_document_id);

create table conclusion_file
(
    id                   serial
        primary key,
    conclusion_id        integer not null
        references conclusion
            on delete cascade,
    outgoing_document_id integer not null
        references outgoing_document
            on delete cascade,
    attachment_order     integer,
    created_at           timestamp with time zone,
    updated_at           timestamp with time zone,
    created_by           integer,
    updated_by           integer
);

comment on table conclusion_file is 'Files attached to conclusions';

alter table conclusion_file
    owner to postgres;

create table document_signature
(
    id                   serial
        primary key,
    incoming_document_id integer
        references incoming_document
            on delete cascade,
    outgoing_document_id integer
        references outgoing_document
            on delete cascade,
    user_id              integer                  not null
        references user_account
            on delete cascade,
    signature_type       text,
    signature_stamp      text,
    signed_at            timestamp with time zone not null,
    ip_address           text,
    comment              text,
    created_at           timestamp with time zone,
    updated_at           timestamp with time zone,
    created_by           integer,
    updated_by           integer,
    constraint document_signature_check
        check (((incoming_document_id IS NOT NULL) AND (outgoing_document_id IS NULL)) OR
               ((incoming_document_id IS NULL) AND (outgoing_document_id IS NOT NULL)))
);

comment on table document_signature is 'Digital signatures on documents';

alter table document_signature
    owner to postgres;

create index idx_document_signature_user_id
    on document_signature (user_id);

create index idx_document_signature_signed_at
    on document_signature (signed_at);

create table status_history
(
    id                  serial
        primary key,
    approval_request_id integer                                not null
        references approval_request
            on delete cascade,
    previous_status     text,
    new_status          text,
    comment             text,
    changed_by          integer
                                                               references user_account
                                                                   on delete set null,
    changed_at          timestamp with time zone default now() not null,
    created_at          timestamp with time zone,
    updated_at          timestamp with time zone,
    created_by          integer,
    updated_by          integer
);

comment on table status_history is 'Status change history';

alter table status_history
    owner to postgres;

create index idx_status_history_approval_request_id
    on status_history (approval_request_id);

create index idx_status_history_changed_at
    on status_history (changed_at);

create table comment
(
    id                  serial
        primary key,
    approval_request_id integer               not null
        references approval_request
            on delete cascade,
    text                text,
    parent_comment_id   integer
        references comment
            on delete cascade,
    author_id           integer               not null
        references user_account
            on delete cascade,
    is_internal         boolean default false not null,
    created_at          timestamp with time zone,
    updated_at          timestamp with time zone,
    created_by          integer,
    updated_by          integer
);

comment on table comment is 'Comments on approval requests';

alter table comment
    owner to postgres;

create index idx_comment_approval_request_id
    on comment (approval_request_id);

create index idx_comment_author_id
    on comment (author_id);

create index idx_comment_parent_comment_id
    on comment (parent_comment_id);

create table notification
(
    id                  serial
        primary key,
    user_id             integer               not null
        references user_account
            on delete cascade,
    approval_request_id integer
                                              references approval_request
                                                  on delete set null,
    type                text,
    title               text,
    message             text,
    is_read             boolean default false not null,
    read_at             timestamp with time zone,
    sent_at             timestamp with time zone,
    created_at          timestamp with time zone,
    updated_at          timestamp with time zone,
    created_by          integer,
    updated_by          integer
);

comment on table notification is 'User notifications';

alter table notification
    owner to postgres;

create index idx_notification_user_id
    on notification (user_id);

create index idx_notification_is_read
    on notification (is_read);

create index idx_notification_created_at
    on notification (created_at);

create table organization_setting
(
    id              serial
        primary key,
    organization_id integer not null
        references organization
            on delete cascade,
    setting_key     text    not null,
    setting_value   text,
    data_type       text,
    description     text,
    created_at      timestamp with time zone,
    updated_at      timestamp with time zone,
    created_by      integer,
    updated_by      integer,
    unique (organization_id, setting_key)
);

comment on table organization_setting is 'Organization-specific settings';

alter table organization_setting
    owner to postgres;

create table action_log
(
    id              serial
        primary key,
    user_id         integer
                                                           references user_account
                                                               on delete set null,
    organization_id integer
                                                           references organization
                                                               on delete set null,
    action          text,
    entity_type     text,
    entity_id       integer,
    old_value       jsonb,
    new_value       jsonb,
    ip_address      text,
    user_agent      text,
    action_at       timestamp with time zone default now() not null,
    created_at      timestamp with time zone,
    updated_at      timestamp with time zone,
    created_by      integer,
    updated_by      integer
);

comment on table action_log is 'Audit log of user actions';

alter table action_log
    owner to postgres;

create index idx_action_log_user_id
    on action_log (user_id);

create index idx_action_log_organization_id
    on action_log (organization_id);

create index idx_action_log_action_at
    on action_log (action_at);

create index idx_action_log_entity_type_entity_id
    on action_log (entity_type, entity_id);


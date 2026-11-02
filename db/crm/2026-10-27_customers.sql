SET client_encoding = 'UTF8';

CREATE TABLE customers
(
    cus_id uuid PRIMARY KEY,
    cus_subject_id uuid NOT NULL UNIQUE,
    cus_name text NOT NULL
);

CREATE TABLE prostheses
(
    pro_id integer PRIMARY KEY,
    pro_customer_id uuid NOT NULL REFERENCES customers (cus_id)
);

CREATE INDEX prostheses_customer_idx ON prostheses (pro_customer_id);

COMMENT ON TABLE customers IS 'Клиенты учебной CRM';
COMMENT ON COLUMN customers.cus_id IS 'Идентификатор клиента';
COMMENT ON COLUMN customers.cus_subject_id IS 'Идентификатор пользователя в Keycloak';
COMMENT ON COLUMN customers.cus_name IS 'Имя клиента для отчёта';

COMMENT ON TABLE prostheses IS 'Протезы клиентов';
COMMENT ON COLUMN prostheses.pro_id IS 'Идентификатор протеза';
COMMENT ON COLUMN prostheses.pro_customer_id IS 'Владелец протеза';

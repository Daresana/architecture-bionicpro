import hashlib
import json
import os
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

import psycopg2
import requests
from airflow import DAG
from airflow.hooks.base import BaseHook
from airflow.operators.python import PythonOperator


def get_connection(connection_id):
    connection = BaseHook.get_connection(connection_id)

    return psycopg2.connect(
        host=connection.host,
        port=connection.port,
        dbname=connection.schema,
        user=connection.login,
        password=connection.password,
        connect_timeout=10
    )


def extract_data(**context):
    start = context['data_interval_start']
    end = context['data_interval_end']
    batch_id = hashlib.sha256(context['run_id'].encode()).hexdigest()

    directory = Path('/opt/airflow/logs/etl') / batch_id
    directory.mkdir(parents=True, exist_ok=True)

    with get_connection('crm') as connection:
        with connection.cursor() as cursor:
            cursor.execute('''
                SELECT pro_id, cus_subject_id::text, cus_name
                FROM prostheses
                JOIN customers ON cus_id = pro_customer_id
            ''')
            customers = cursor.fetchall()

    with get_connection('telemetry') as connection:
        with connection.cursor() as cursor:
            cursor.execute('''
                SELECT mea_prosthesis_id, mea_created_at, mea_movement_count,
                    mea_signal, mea_battery
                FROM measurements
                WHERE mea_created_at >= %s AND mea_created_at < %s
                ORDER BY mea_created_at, mea_id
            ''', (start, end))
            measurements = cursor.fetchall()

    source = {
        'report_date': start.date().isoformat(),
        'batch_id': batch_id,
        'customers': customers,
        'measurements': measurements
    }

    path = directory / 'source.json'
    path.write_text(json.dumps(source, default=str, ensure_ascii=False), encoding='utf-8')

    return str(path)


def transform_data(**context):
    path = Path(context['ti'].xcom_pull(task_ids='extract'))
    source = json.loads(path.read_text(encoding='utf-8'))
    customers = {}
    totals = {}

    for prosthesis_id, user_id, name in source['customers']:
        customers[prosthesis_id] = (user_id, name)

    # Объединение источников и расчёт суточных показателей.
    for prosthesis_id, measured_at, movements, signal, battery in source['measurements']:
        if prosthesis_id not in customers:
            raise ValueError('Для протеза не найден владелец в CRM')

        user_id, name = customers[prosthesis_id]

        if prosthesis_id not in totals:
            totals[prosthesis_id] = {
                'user_id': user_id,
                'report_date': source['report_date'],
                'prosthesis_id': prosthesis_id,
                'client_name': name,
                'movement_count': 0,
                'signal_sum': 0,
                'battery_sum': 0,
                'sample_count': 0
            }

        row = totals[prosthesis_id]
        row['movement_count'] += movements
        row['signal_sum'] += signal
        row['battery_sum'] += battery
        row['sample_count'] += 1

    rows = []

    for row in totals.values():
        row['average_signal'] = round(row.pop('signal_sum') / row['sample_count'], 4)
        row['average_battery'] = round(row.pop('battery_sum') / row.pop('sample_count'), 2)
        row['batch_id'] = source['batch_id']
        rows.append(row)

    result = {
        'report_date': source['report_date'],
        'batch_id': source['batch_id'],
        'rows': rows
    }

    output = path.parent / 'report.json'
    output.write_text(json.dumps(result, ensure_ascii=False), encoding='utf-8')

    return str(output)


def insert_rows(table, rows):
    if not rows:
        return

    body = '\n'.join(json.dumps(row, ensure_ascii=False) for row in rows)
    query = f'INSERT INTO reports.{table} FORMAT JSONEachRow\n{body}'

    response = requests.post(
        os.environ['CLICKHOUSE_URL'],
        data=query.encode('utf-8'),
        auth=(os.environ['CLICKHOUSE_USER'], os.environ['CLICKHOUSE_PASSWORD']),
        timeout=30
    )
    response.raise_for_status()


def load_data(**context):
    path = Path(context['ti'].xcom_pull(task_ids='transform'))
    result = json.loads(path.read_text(encoding='utf-8'))
    version = time.time_ns()

    for row in result['rows']:
        row['version'] = version

    insert_rows('usage_reports', result['rows'])

    # Период публикуется после полной загрузки витрины.
    insert_rows('report_periods', [{
        'report_date': result['report_date'],
        'batch_id': result['batch_id'],
        'version': version
    }])


with DAG(
    dag_id='prosthesis_reports',
    start_date=datetime(2024, 12, 1, tzinfo=timezone.utc),
    schedule='@daily',
    catchup=False,
    max_active_runs=1,
    default_args={'retries': 2, 'retry_delay': timedelta(minutes=1)}
) as dag:
    extract = PythonOperator(task_id='extract', python_callable=extract_data)
    transform = PythonOperator(task_id='transform', python_callable=transform_data)
    load = PythonOperator(task_id='load', python_callable=load_data)

    extract >> transform >> load
